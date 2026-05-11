using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Dapper;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Pages;
using EnterpriseWorkReport.Views.Dialogs;

namespace EnterpriseWorkReport.Views
{
    public partial class MainShellWindow : Window
    {
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _inactivityTimer;
        private Button _activeNavButton;
        private readonly Dictionary<string, Button> _navButtons;
        private AutoImportService _autoImportService;

        public MainShellWindow()
        {
            InitializeComponent();
            LoadCompanyInfo();
            LoadUserProfile();
            LoadWallpaper();

            // Hide admin section for non-admins
            if (!SessionManager.IsAdmin)
            {
                AdminSection.Visibility = Visibility.Collapsed;
                NavBulkOps.Visibility = Visibility.Collapsed;
                NavUsers.Visibility = Visibility.Collapsed;
                NavAnalytics.Visibility = Visibility.Collapsed;
            }

            // Build nav button map
            _navButtons = new Dictionary<string, Button>
            {
                { "Dashboard", NavDashboard },
                { "Projects", NavProjects },
                { "WorkReports", NavWorkReports },
                { "Billing", NavBilling },
                { "Attendance", NavAttendance },
                { "Leave", NavLeave },
                { "Quality", NavQuality },
                { "Analytics", NavAnalytics },
                { "Leaderboard", NavLeaderboard },
                { "Messages", NavMessages },
                { "BulkOps", NavBulkOps },
                { "Users", NavUsers },
                { "Resumes", NavResumes },
                { "Settings", NavSettings }
            };

            _activeNavButton = NavDashboard;

            // Start clock
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();

            // Intercept input for 30 min session timeout
            InputManager.Current.PreProcessInput += OnActivity;
            _inactivityTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _inactivityTimer.Tick += OnInactivityTimeout;
            _inactivityTimer.Start();

            // Start AutoImport for admin users
            if (SessionManager.IsAdmin)
            {
                StartAutoImport();
            }

            // Initialize Notification Service
            NotificationService.Initialize(NotificationContainer);

            // Initialize Auto-Backup
            AutoBackupService.Initialize();

            // Start Background Sync for admin users
            if (SessionManager.IsAdmin)
            {
                BackgroundSyncService.Instance.Start();
            }

            NavigateTo("Dashboard");

            // Check for onboarding
            CheckForOnboarding();
        }

        private int _onboardingStep = 0;
        private void CheckForOnboarding()
        {
            // Simple check in DB or settings
            using (var conn = DatabaseService.GetConnection())
            {
                var userId = SessionManager.CurrentUser?.Id ?? 0;
                var hasCompleted = conn.ExecuteScalar<bool>("SELECT EXISTS(SELECT 1 FROM UserSettings WHERE UserId=@U AND Key='OnboardingCompleted' AND Value='True')", new { U = userId });
                
                if (!hasCompleted)
                {
                    ShowOnboardingStep(0);
                }
            }
        }

        private void ShowOnboardingStep(int step)
        {
            _onboardingStep = step;
            OnboardingOverlay.Visibility = Visibility.Visible;

            switch (step)
            {
                case 0:
                    OnboardingTitle.Text = "Welcome to Enterprise Work Report! 🚀";
                    OnboardingMessage.Text = "Let's take a 30-second tour to get you started with your new productivity dashboard.";
                    OnboardingNextBtn.Content = "Start Tour";
                    break;
                case 1:
                    OnboardingTitle.Text = "Quick Clock-In ⏰";
                    OnboardingMessage.Text = "You can clock in and out directly from the Dashboard. We'll track your hours automatically!";
                    OnboardingNextBtn.Content = "Next";
                    NavigateTo("Dashboard");
                    break;
                case 2:
                    OnboardingTitle.Text = "Project History 📁";
                    OnboardingMessage.Text = "View all your assigned projects and their details here. Tracking your work has never been easier.";
                    OnboardingNextBtn.Content = "Next";
                    NavigateTo("Projects");
                    break;
                case 3:
                    OnboardingTitle.Text = "Personal Analytics 📊";
                    OnboardingMessage.Text = "Check your performance trends, quality scores, and billing history in the Analytics section.";
                    OnboardingNextBtn.Content = "Finish";
                    NavigateTo("Analytics");
                    break;
            }
        }

        private void NextOnboarding_Click(object sender, RoutedEventArgs e)
        {
            if (_onboardingStep < 3)
            {
                ShowOnboardingStep(_onboardingStep + 1);
            }
            else
            {
                FinishOnboarding();
            }
        }

        private void SkipOnboarding_Click(object sender, RoutedEventArgs e)
        {
            FinishOnboarding();
        }

        private void FinishOnboarding()
        {
            OnboardingOverlay.Visibility = Visibility.Collapsed;
            using (var conn = DatabaseService.GetConnection())
            {
                var userId = SessionManager.CurrentUser?.Id ?? 0;
                conn.Execute("INSERT INTO UserSettings (UserId, Key, Value) VALUES (@U, 'OnboardingCompleted', 'True') ON CONFLICT(UserId, Key) DO UPDATE SET Value='True'", new { U = userId });
            }
            NotificationService.Show("Tour Completed", "You're all set! Enjoy using the system.", NotificationType.Success);
            NavigateTo("Dashboard");
        }

        public void LoadCompanyInfo()
        {
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var settings = Dapper.SqlMapper.QueryFirstOrDefault<EnterpriseWorkReport.Models.CompanySettings>(
                        conn, "SELECT * FROM CompanySettings WHERE Id = 1");
                    
                    if (settings != null)
                    {
                        CompanyNameText.Text = string.IsNullOrWhiteSpace(settings.CompanyName) ? "WorkReport" : settings.CompanyName;
                        
                        if (!string.IsNullOrWhiteSpace(settings.LogoPath) && System.IO.File.Exists(settings.LogoPath))
                        {
                            var uri = new Uri(settings.LogoPath, UriKind.Absolute);
                            CompanyLogoImage.Source = new System.Windows.Media.Imaging.BitmapImage(uri);
                            CompanyLogoImage.Visibility = Visibility.Visible;
                            CompanyLogoTextFallback.Visibility = Visibility.Collapsed;
                            CompanyLogoBorder.Background = System.Windows.Media.Brushes.Transparent;
                        }
                    }
                }
            }
            catch { /* Ignore database load issues silently or log */ }
        }

        public void LoadUserProfile()
        {
            var user = SessionManager.CurrentUser;
            if (user == null) return;

            UserFullNameText.Text = user.FullName ?? "User";
            UserRoleText.Text = user.Role ?? "Employee";
            UserInitialText.Text = (user.FullName?.Length > 0) ? user.FullName[0].ToString().ToUpper() : "U";

            if (!string.IsNullOrWhiteSpace(user.ProfilePicture) && System.IO.File.Exists(user.ProfilePicture))
            {
                try 
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(user.ProfilePicture, UriKind.Absolute);
                    bmp.EndInit();
                    UserProfileImage.Source = bmp;
                    UserProfileImage.Visibility = Visibility.Visible;
                } 
                catch { }
            }
        }

        private void LoadWallpaper()
        {
            string wallpaper = SessionManager.CurrentUser?.WallpaperPath;
            
            // Fallback to first wallpaper if none selected
            if (string.IsNullOrEmpty(wallpaper))
            {
                wallpaper = WallpaperService.GetFirstWallpaper();
            }

            if (!string.IsNullOrEmpty(wallpaper) && System.IO.File.Exists(wallpaper))
            {
                try
                {
                    SetBackgroundImage(new Uri(wallpaper, UriKind.Absolute));
                }
                catch { }
            }
        }



        private void BackgroundImage_ImageFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
        {
            BackgroundImage.Visibility = Visibility.Collapsed;
        }

        private void OnActivity(object sender, PreProcessInputEventArgs e)
        {
            if (e.StagingItem.Input.RoutedEvent == Keyboard.KeyDownEvent || e.StagingItem.Input.RoutedEvent == Mouse.MouseMoveEvent)
            {
                _inactivityTimer.Stop();
                _inactivityTimer.Start();
            }
        }

        private void OnInactivityTimeout(object sender, EventArgs e)
        {
            _inactivityTimer.Stop();
            InputManager.Current.PreProcessInput -= OnActivity;
            MessageBox.Show("Your session has timed out due to 30 minutes of inactivity.", "Session Timeout", MessageBoxButton.OK, MessageBoxImage.Warning);
            SignOut_Click(null, null);
        }



        private void UpdateClock()
        {
            DateTimeText.Text = DateTime.Now.ToString("dddd, dd MMM yyyy   hh:mm tt");
            // Update maximize button icon based on window state
            MaximizeBtn.Content = WindowState == WindowState.Maximized ? "❐" : "☐";
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            var helpDialog = new HelpDialog { Owner = this };
            helpDialog.ShowDialog();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            // Update maximize button icon when window state changes
            MaximizeBtn.Content = WindowState == WindowState.Maximized ? "❐" : "☐";
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string tag = btn.Tag?.ToString();
                NavigateTo(tag);
            }
        }

        public void NavigateTo(string page)
        {
            // Update nav button styles
            if (_activeNavButton != null)
                _activeNavButton.Style = (Style)FindResource("SidebarNavButton");

            if (_navButtons.TryGetValue(page, out var navBtn))
            {
                navBtn.Style = (Style)FindResource("SidebarNavButtonActive");
                _activeNavButton = navBtn;
            }

            // Navigate to page
            Page targetPage = null;
            string titleText = page;

            switch (page)
            {
                case "Dashboard":
                    targetPage = new DashboardPage();
                    titleText = "Dashboard";
                    break;
                case "Projects":
                    targetPage = new ProjectsPage();
                    titleText = "Projects";
                    break;
                case "WorkReports":
                    targetPage = new WorkReportsPage();
                    titleText = "Work Reports";
                    break;
                case "Billing":
                    targetPage = new BillingPage();
                    titleText = "Billing";
                    break;
                case "Attendance":
                    targetPage = new AttendancePage();
                    titleText = "Attendance";
                    break;
                case "Leave":
                    targetPage = new LeavePage();
                    titleText = "Leave Management";
                    break;
                case "Quality":
                    targetPage = new QualityPage();
                    titleText = "Quality Reports";
                    break;
                case "Analytics":
                    targetPage = new AnalyticsPage();
                    titleText = "Analytics";
                    break;
                case "Leaderboard":
                    targetPage = new LeaderboardPage();
                    titleText = "Leaderboard";
                    break;
                case "Messages":
                    targetPage = new MessagesPage();
                    titleText = "Messages";
                    break;
                case "BulkOps":
                    targetPage = new BulkOpsPage();
                    titleText = "Bulk Operations";
                    break;
                case "Users":
                    targetPage = new UsersPage();
                    titleText = "User Management";
                    break;
                case "Resumes":
                    targetPage = new ResumesPage();
                    titleText = "Candidate Resumes";
                    break;
                case "Settings":
                    targetPage = new SettingsPage();
                    titleText = "Settings";
                    break;
            }

            if (targetPage != null)
            {
                MainFrame.Navigate(targetPage);
                PageTitleBar.Text = titleText;
            }
        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            _clockTimer.Stop();
            SessionManager.Logout();
            var loginWin = new LoginWindow();
            Application.Current.MainWindow = loginWin;
            loginWin.Show();
            this.Close();
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            var profileDialog = new ProfileDialog { Owner = this };
            profileDialog.ShowDialog();
            
            // Refresh user info after profile update
            var user = SessionManager.CurrentUser;
            if (user != null)
            {
                UserFullNameText.Text = user.FullName;
                UserInitialText.Text = (user.FullName?.Length > 0) ? user.FullName[0].ToString().ToUpper() : "U";
            }
        }

        // ---- Global Search Bar Handlers ----
        private void GlobalSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (GlobalSearchBox.Text == "Search (Press Enter)...")
            {
                GlobalSearchBox.Text = "";
                GlobalSearchBox.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush");
            }
        }

        private void GlobalSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GlobalSearchBox.Text))
            {
                GlobalSearchBox.Text = "Search (Press Enter)...";
                GlobalSearchBox.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)); // Gray 400
            }
        }

        private void GlobalSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string query = GlobalSearchBox.Text.Trim();
                if (!string.IsNullOrEmpty(query) && query != "Search (Press Enter)...")
                {
                    // Update nav button state
                    if (_activeNavButton != null)
                        _activeNavButton.Style = (Style)FindResource("SidebarNavButton");
                    if (_navButtons.TryGetValue("WorkReports", out var navBtn2))
                    {
                        navBtn2.Style = (Style)FindResource("SidebarNavButtonActive");
                        _activeNavButton = navBtn2;
                    }

                    // Create the page, pre-load data, then apply the search filter
                    var wrPage = new WorkReportsPage();
                    wrPage.Search(query);
                    MainFrame.Navigate(wrPage);
                    PageTitleBar.Text = "Work Reports";

                    // Reset the search box
                    GlobalSearchBox.Text = "Search (Press Enter)...";
                    GlobalSearchBox.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(156, 163, 175));
                }
            }
        }

        private void StartAutoImport()
        {
            try
            {
                string watchFolder = System.IO.Path.Combine(DatabaseService.AppDataFolder, "MasterFiles");
                if (!System.IO.Directory.Exists(watchFolder))
                    System.IO.Directory.CreateDirectory(watchFolder);

                _autoImportService = new AutoImportService(watchFolder);
                _autoImportService.FileImported += (s, msg) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        AuditService.Log("AutoImport", msg);
                    });
                };
                _autoImportService.ImportError += (s, msg) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        AuditService.Log("AutoImport Error", msg);
                    });
                };
                _autoImportService.Start();
            }
            catch (Exception ex)
            {
                AuditService.Log("AutoImport Init Failed", ex.Message);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _clockTimer.Stop();
            if (_inactivityTimer != null) _inactivityTimer.Stop();
            _autoImportService?.Dispose();
            BackgroundSyncService.Instance.Stop();
            InputManager.Current.PreProcessInput -= OnActivity;
            base.OnClosed(e);
        }

        public void SetBackgroundImage(Uri imageUri)
        {
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.UriSource = imageUri;
                bitmap.EndInit();
                bitmap.Freeze();
                BackgroundImage.Source = bitmap;
                BackgroundImage.Visibility = Visibility.Visible;
                BackgroundImage.Opacity = 0.15;
            }
            catch { }
        }

        public void ClearBackgroundImage()
        {
            BackgroundImage.Source = null;
            BackgroundImage.Visibility = Visibility.Collapsed;
        }
    }
}
