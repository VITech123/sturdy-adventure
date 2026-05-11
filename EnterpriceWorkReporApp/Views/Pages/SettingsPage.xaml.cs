using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using System.Linq;
using Microsoft.Win32;
using System.Collections.Generic;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class SettingsPage : Page
    {
        private readonly LanServerService _lanServer;
        private string _logoPath = null;

        public SettingsPage()
        {
            InitializeComponent();
            
            // Restrict admin-only features for non-admin users
            if (!SessionManager.IsAdmin)
            {
                AuditLogSection.Visibility = Visibility.Collapsed;
                CompanySettingsSection.Visibility = Visibility.Collapsed;
                LanServerSection.Visibility = Visibility.Collapsed;
                BackupSection.Visibility = Visibility.Collapsed;
                BackgroundSyncSection.Visibility = Visibility.Collapsed;
            }
            
            // Load admin data only if admin
            if (SessionManager.IsAdmin)
            {
                LoadAuditLog();
                LoadCompanySettings();
                _lanServer = new LanServerService(5050);
                _lanServer.LogMessage += OnLanServerLog;
                
                BackgroundSyncSection.Visibility = Visibility.Visible;
                BackgroundSyncService.Instance.SyncLog += OnSyncLog;
            }
            else
            {
                BackgroundSyncSection.Visibility = Visibility.Collapsed;
            }
            
            LoadWallpapers();
        }

        private void OnSyncLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
                SyncStatusText.Text = formatted;
                if (SyncLogTextBox != null)
                {
                    SyncLogTextBox.Text = formatted + Environment.NewLine + SyncLogTextBox.Text;
                }
            });
        }

        private async void ManualSync_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SyncStatusText.Text = "Starting manual synchronization...";
                await BackgroundSyncService.Instance.PerformSync();
                LoadProjectPaths();
            }
            catch (Exception ex)
            {
                SyncStatusText.Text = $"Manual sync failed: {ex.Message}";
            }
        }

        private void LoadWallpapers()
        {
            try
            {
                var wallpapers = WallpaperService.GetAvailableWallpapers();
                var userWallpaper = SessionManager.CurrentUser?.WallpaperPath;
                
                var items = new System.Collections.Generic.List<WallpaperItem>();
                foreach (var wp in wallpapers)
                {
                    items.Add(new WallpaperItem
                    {
                        FullPath = wp,
                        FileName = Path.GetFileName(wp),
                        ImageSource = WallpaperService.LoadWallpaperImage(wp),
                        IsSelected = wp == userWallpaper
                    });
                }
                
                WallpaperGallery.ItemsSource = items;
                WallpaperEnabledCheck.IsChecked = !string.IsNullOrEmpty(userWallpaper);
            }
            catch (Exception ex)
            {
                WallpaperStatusText.Text = "Error loading wallpapers: " + ex.Message;
            }
        }

        private void Wallpaper_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is WallpaperItem item)
            {
                var items = WallpaperGallery.ItemsSource as System.Collections.Generic.List<WallpaperItem>;
                if (items != null)
                {
                    foreach (var wp in items) wp.IsSelected = false;
                    item.IsSelected = true;
                    
                    // Force refresh UI
                    WallpaperGallery.ItemsSource = null;
                    WallpaperGallery.ItemsSource = items;
                    
                    // Preview immediately
                    if (Window.GetWindow(this) is MainShellWindow shell)
                    {
                        shell.SetBackgroundImage(new Uri(item.FullPath, UriKind.Absolute));
                    }
                }
            }
        }

        private void SaveWallpaperChoice_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = (WallpaperGallery.ItemsSource as System.Collections.Generic.List<WallpaperItem>)?
                .FirstOrDefault(i => i.IsSelected);
            
            string path = selectedItem?.FullPath;
            if (WallpaperEnabledCheck.IsChecked == false) path = null;

            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    conn.Execute("UPDATE Users SET WallpaperPath = @Path WHERE Id = @Id", 
                        new { Path = path, Id = SessionManager.CurrentUser.Id });
                }
                
                // Update session
                SessionManager.CurrentUser.WallpaperPath = path;
                
                WallpaperStatusText.Text = "✓ Wallpaper preferences saved!";
                AuditService.Log("Settings", "Updated dashboard wallpaper");
            }
            catch (Exception ex)
            {
                WallpaperStatusText.Text = "✕ Error saving: " + ex.Message;
            }
        }

        private void LoadCompanySettings()
        {
            var companyService = new CompanyService();
            var settings = companyService.GetSettings();
            if (settings != null)
            {
                CompanyNameBox.Text = settings.CompanyName ?? "";
                CompanyEmailBox.Text = settings.CompanyEmail ?? "";
                CompanyPhoneBox.Text = settings.CompanyPhone ?? "";
                CompanyAddressBox.Text = settings.CompanyAddress ?? "";
                TaxIdBox.Text = settings.TaxId ?? "";
                CurrencyBox.Text = settings.CurrencySymbol ?? "₹";
                MasterNamesPathBox.Text = settings.MasterNamesPath ?? "";
                
                _logoPath = settings.LogoPath;
                if (!string.IsNullOrWhiteSpace(_logoPath) && File.Exists(_logoPath))
                {
                    try
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(_logoPath, UriKind.Absolute);
                        bmp.EndInit();
                        SettingsLogoImage.Source = bmp;
                    }
                    catch { }
                }
            }
            
            LoadProjectPaths();
        }

        private void SaveCompanySettings_Click(object sender, RoutedEventArgs e)
        {
            // Security check: Only admins can save company settings
            if (!SessionManager.IsAdmin)
            {
                MessageBox.Show("Access Denied. Only administrators can modify company settings.", 
                    "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                var companyService = new CompanyService();
                var settings = new CompanySettings
                {
                    CompanyName = CompanyNameBox.Text.Trim(),
                    CompanyEmail = CompanyEmailBox.Text.Trim(),
                    CompanyPhone = CompanyPhoneBox.Text.Trim(),
                    CompanyAddress = CompanyAddressBox.Text.Trim(),
                    TaxId = TaxIdBox.Text.Trim(),
                    CurrencySymbol = CurrencyBox.Text.Trim(),
                    LogoPath = _logoPath,
                    MasterNamesPath = MasterNamesPathBox.Text.Trim()
                };
                companyService.UpdateSettings(settings);
                
                // Refresh shell window if it's currently open
                if (Window.GetWindow(this) is MainShellWindow shell)
                {
                    shell.LoadCompanyInfo();
                }

                CompanyStatusText.Text = "✓ Company settings saved successfully!";
                CompanyStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                CompanyStatusText.Text = $"✕ Error: {ex.Message}";
                CompanyStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CurrentPasswordBox.Password))
            {
                PasswordStatusText.Text = "✕ Current password is required.";
                PasswordStatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            if (NewPasswordBox.Password.Length < 4)
            {
                PasswordStatusText.Text = "✕ New password must be at least 4 characters.";
                PasswordStatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
            {
                PasswordStatusText.Text = "✕ New passwords do not match.";
                PasswordStatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            try
            {
                var authService = new AuthService();
                bool success = authService.ChangePassword(SessionManager.CurrentUser.Id, CurrentPasswordBox.Password, NewPasswordBox.Password);

                if (success)
                {
                    PasswordStatusText.Text = "✓ Password updated successfully!";
                    PasswordStatusText.Foreground = System.Windows.Media.Brushes.Green;
                    CurrentPasswordBox.Password = "";
                    NewPasswordBox.Password = "";
                    ConfirmPasswordBox.Password = "";
                }
                else
                {
                    PasswordStatusText.Text = "✕ Current password incorrect.";
                    PasswordStatusText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                PasswordStatusText.Text = $"✕ Error: {ex.Message}";
                PasswordStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void UploadLogo_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsAdmin) return;
            
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select Company Logo"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string uploadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnterpriseWorkReport", "Uploads", "Images");
                    Directory.CreateDirectory(uploadsFolder);
                    
                    string ext = Path.GetExtension(dlg.FileName);
                    string targetPath = Path.Combine(uploadsFolder, $"company_logo_{DateTime.Now.Ticks}{ext}");
                    File.Copy(dlg.FileName, targetPath, true);
                    
                    _logoPath = targetPath;
                    
                    // Show in preview
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(targetPath, UriKind.Absolute);
                    bmp.EndInit();
                    SettingsLogoImage.Source = bmp;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to upload logo: {ex.Message}", "Error");
                }
            }
        }

        private void BrowseMasterNames_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls|All Files|*.*",
                Title = "Select Master Names File"
            };
            if (dlg.ShowDialog() == true)
                MasterNamesPathBox.Text = dlg.FileName;
        }

        private FuzzyNameMatcher GetFuzzyMatcher()
        {
            var matcher = new FuzzyNameMatcher();
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var settings = conn.QueryFirstOrDefault<CompanySettings>(
                        "SELECT MasterNamesPath FROM CompanySettings WHERE Id = 1");
                    if (settings != null && !string.IsNullOrWhiteSpace(settings.MasterNamesPath))
                        matcher.LoadMasterNames(settings.MasterNamesPath);
                }
            }
            catch { }
            return matcher;
        }

        private void LoadProjectPaths()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var projects = conn.Query<Project>("SELECT Id, Name, MasterFilePath, MasterFilePassword, QualityBasePath, LastMasterSyncAt, LastMasterSyncStatus FROM Projects ORDER BY Name").ToList();
                foreach (var p in projects)
                {
                    p.MasterFilePassword = SecretService.DecryptSecret(p.MasterFilePassword);
                }
                ProjectPathsGrid.ItemsSource = projects;
            }
        }

        private void BrowseMasterFile_GridClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var project = button?.Tag as Project;
            if (project == null) return;

            var dlg = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls|All Files|*.*",
                Title = $"Select Master File for {project.Name}"
            };
            if (dlg.ShowDialog() == true)
            {
                project.MasterFilePath = dlg.FileName;
                ProjectPathsGrid.Items.Refresh();
            }
        }

        private void BrowseQualityPath_GridClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var project = button?.Tag as Project;
            if (project == null) return;

            var dlg = new OpenFileDialog
            {
                Title = $"Select Quality Base Folder for {project.Name}",
                CheckFileExists = false,
                FileName = "Select Folder",
                Filter = "Folders|*.none",
                ValidateNames = false
            };
            
            if (dlg.ShowDialog() == true)
            {
                project.QualityBasePath = Path.GetDirectoryName(dlg.FileName);
                ProjectPathsGrid.Items.Refresh();
            }
        }

        private void SaveProjectPaths_Click(object sender, RoutedEventArgs e)
        {
            var projects = ProjectPathsGrid.ItemsSource as List<Project>;
            if (projects == null) return;

            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    foreach (var p in projects)
                    {
                        conn.Execute(@"UPDATE Projects SET 
                                      MasterFilePath = @MasterFilePath, 
                                      MasterFilePassword = @MasterFilePassword, 
                                      QualityBasePath = @QualityBasePath 
                                      WHERE Id = @Id", new
                        {
                            p.MasterFilePath,
                            MasterFilePassword = SecretService.EnsureEncrypted(p.MasterFilePassword),
                            p.QualityBasePath,
                            p.Id
                        });
                    }
                }
                
                ProjectPathStatusText.Text = "✅ All project paths saved successfully!";
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, ev) => { ProjectPathStatusText.Text = ""; timer.Stop(); };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save project paths: {ex.Message}", "Error");
            }
        }

        private void SyncProjectNow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not Project project)
                return;

            if (string.IsNullOrWhiteSpace(project.MasterFilePath) || !File.Exists(project.MasterFilePath))
            {
                MessageBox.Show($"Master file path for project '{project.Name}' is invalid or missing.", "Sync Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            var originalText = button?.Content;
            try
            {
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Syncing...";
                }

                var masterService = new MasterDataService();
                var nameMatcher = GetFuzzyMatcher();
                int imported = masterService.SyncMasterFile(project.Id, project.MasterFilePath, false,
                    string.IsNullOrWhiteSpace(project.MasterFilePassword) ? null : project.MasterFilePassword,
                    nameMatcher);

                string status = imported > 0
                    ? $"Manual sync imported {imported} records"
                    : "Manual sync found no changes";

                DatabaseService.UpdateProjectSyncStatus(project.Id, DateTime.Now, status);
                LoadProjectPaths();

                MessageBox.Show($"Project '{project.Name}' synced successfully.\n{status}", "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DatabaseService.UpdateProjectSyncStatus(project.Id, DateTime.Now, $"Manual sync failed: {ex.Message}");
                MessageBox.Show($"Sync failed for project '{project.Name}': {ex.Message}", "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = originalText;
                }
            }
        }

        private void OnLanServerLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ServerStatusText.Text = message;
                if (_lanServer.IsRunning)
                {
                    UpdateServerUI(true);
                }
            });
        }

        private void UpdateServerUI(bool isRunning)
        {
            if (isRunning)
            {
                ServerStatusText.Text = "✓ Server is running";
                ServerStatusText.Foreground = System.Windows.Media.Brushes.Green;
                StartServerBtn.Visibility = Visibility.Collapsed;
                StopServerBtn.Visibility = Visibility.Visible;
                ServerUrlsPanel.Visibility = Visibility.Visible;
                
                // Show access URLs
                var urls = GetAccessUrls();
                ServerUrlsText.Text = string.Join("\n", urls);
            }
            else
            {
                ServerStatusText.Text = "Server is NOT running";
                ServerStatusText.Foreground = System.Windows.Media.Brushes.Gray;
                StartServerBtn.Visibility = Visibility.Visible;
                StopServerBtn.Visibility = Visibility.Collapsed;
                ServerUrlsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private string[] GetAccessUrls()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var urls = new System.Collections.Generic.List<string>();
                
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        urls.Add($"http://{ip}:5050");
                    }
                }
                
                urls.Insert(0, "http://localhost:5050");
                return urls.ToArray();
            }
            catch
            {
                return new[] { "http://localhost:5050" };
            }
        }

        private void LoadAuditLog()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var logs = conn.Query<AuditLog>("SELECT * FROM AuditLogs ORDER BY Timestamp DESC LIMIT 200");
                AuditGrid.ItemsSource = logs;
            }
        }

        private void RefreshAuditLog_Click(object sender, RoutedEventArgs e) => LoadAuditLog();

        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _lanServer.Start();
                UpdateServerUI(true);
                AuditService.Log("LAN Server", "Network server started");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            _lanServer.Stop();
            UpdateServerUI(false);
            AuditService.Log("LAN Server", "Network server stopped");
        }

        private void BackupNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(DatabaseService.BackupsFolder, $"backup_{timestamp}.db");
                File.Copy(DatabaseService.DbPath, backupPath, overwrite: true);
                BackupStatusText.Text = $"✔ Backup created: backup_{timestamp}.db";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Green;

                // Log the action
                AuditService.Log("Backup Created", $"Backup saved to {backupPath}");
                LoadAuditLog();
            }
            catch (Exception ex)
            {
                BackupStatusText.Text = $"✕ Backup failed: {ex.Message}";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Backup File",
                Filter = "SQLite Database|*.db",
                InitialDirectory = DatabaseService.BackupsFolder
            };
            if (dlg.ShowDialog() != true) return;

            var confirm = MessageBox.Show(
                "WARNING: Restoring a backup will replace ALL current data.\n\nAre you sure?",
                "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                File.Copy(dlg.FileName, DatabaseService.DbPath, overwrite: true);
                BackupStatusText.Text = $"✔ Database restored from: {Path.GetFileName(dlg.FileName)}. Please restart the application.";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                BackupStatusText.Text = $"✕ Restore failed: {ex.Message}";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }


        private void ThemeColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string themeName)
            {
                var theme = themeName switch
                {
                    "PastelBlue" => AppTheme.PastelBlue,
                    "PastelGreen" => AppTheme.PastelGreen,
                    "PastelPurple" => AppTheme.PastelPurple,
                    "PastelPink" => AppTheme.PastelPink,
                    "PastelOrange" => AppTheme.PastelOrange,
                    "DarkNavy" => AppTheme.DarkNavy,
                    "AmoledBlack" => AppTheme.AmoledBlack,
                    _ => AppTheme.PastelBlue
                };
                ThemeService.SetTheme(theme);
                CurrentThemeText.Text = $"Current: {themeName.Replace("Pastel", "Pastel ")}";
                AuditService.Log("Theme Changed", $"Theme set to: {themeName}");
            }
        }

        private async void LoadDemo_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsAdmin) return;

            var confirm = MessageBox.Show(
                "CRITICAL WARNING: Loading demo data will PERMANENTLY DELETE all current projects, users, and reports.\n\n" +
                "Are you absolutely sure you want to proceed with a clean reset?",
                "Dangerous Operation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                LoadDemoBtn.IsEnabled = false;
                LoadDemoBtn.Content = "⏳ Generating Data...";
                
                await System.Threading.Tasks.Task.Run(() =>
                {
                    TestDataGenerator.GenerateAllTestData();
                });

                MessageBox.Show("Demo data generated successfully! The system has been reset with a full set of professional sample records.", 
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Refresh audit log to show recent activity
                LoadAuditLog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate demo data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadDemoBtn.IsEnabled = true;
                LoadDemoBtn.Content = "🚀 Load Demo Data";
            }
        }

        private void ClearData_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsAdmin) return;

            var confirm = MessageBox.Show(
                "WARNING: This will delete ALL projects, users, work reports, and master data.\n\n" +
                "The system will be reset to an empty state. Are you sure?",
                "Clear All Data", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                ClearDataBtn.IsEnabled = false;
                ClearDataBtn.Content = "⏳ Clearing...";

                DatabaseService.ClearAllData();

                MessageBox.Show("All data has been cleared. The system is now empty.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ClearDataBtn.IsEnabled = true;
                ClearDataBtn.Content = "🗑️ Clear All Data";
            }
        }

        private void OpenWallpaperFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WallpaperService.EnsureWallpaperFolder();
                System.Diagnostics.Process.Start("explorer.exe", WallpaperService.WallpaperFolder);
                AuditService.Log("Wallpaper", "Opened wallpaper folder");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open folder: {ex.Message}", "Error");
            }
        }

        private void WallpaperEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            
            var mainWindow = Window.GetWindow(this) as MainShellWindow;
            if (mainWindow != null)
            {
                if (WallpaperEnabledCheck.IsChecked == true)
                {
                    var items = WallpaperGallery.ItemsSource as System.Collections.Generic.List<WallpaperItem>;
                    var selected = items?.FirstOrDefault(i => i.IsSelected);
                    if (selected != null)
                    {
                        mainWindow.SetBackgroundImage(new Uri(selected.FullPath, UriKind.Absolute));
                    }
                    else
                    {
                        // Select first by default if nothing selected
                        var first = items?.FirstOrDefault();
                        if (first != null)
                        {
                            first.IsSelected = true;
                            WallpaperGallery.ItemsSource = null;
                            WallpaperGallery.ItemsSource = items;
                            mainWindow.SetBackgroundImage(new Uri(first.FullPath, UriKind.Absolute));
                        }
                    }
                }
                else
                {
                    mainWindow.ClearBackgroundImage();
                }
            }
        }
    }

    public class WallpaperItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        public string FullPath { get; set; }
        public string FileName { get; set; }
        public System.Windows.Media.ImageSource ImageSource { get; set; }
        public bool IsSelected 
        { 
            get => _isSelected; 
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } 
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => 
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
