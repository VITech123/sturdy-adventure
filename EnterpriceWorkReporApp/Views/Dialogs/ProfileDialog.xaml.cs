using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using Microsoft.Win32;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ProfileDialog : Window
    {
        private readonly AuthService _authService = new AuthService();
        private readonly User _currentUser;
        private string _profileImagePath = null;

        public ProfileDialog()
        {
            InitializeComponent();
            _currentUser = SessionManager.CurrentUser;
            LoadUserData();
        }

        private void LoadUserData()
        {
            if (_currentUser == null) return;

            FullNameBox.Text = _currentUser.FullName;
            EmailBox.Text = _currentUser.Email ?? "";
            PhoneBox.Text = _currentUser.Phone ?? "";
            PlaceBox.Text = _currentUser.Place ?? "";
            EducationBox.Text = _currentUser.Education ?? "";
            EmergencyBox.Text = _currentUser.EmergencyNumber ?? "";
            DepartmentBox.Text = _currentUser.Department ?? "";
            DesignationBox.Text = _currentUser.Designation ?? "";
            ProfileInitials.Text = string.IsNullOrEmpty(_currentUser.FullName) ? "?" : _currentUser.FullName.Substring(0, 1).ToUpper();
            
            // Set Theme
            if (!string.IsNullOrEmpty(_currentUser.SelectedTheme))
            {
                foreach (ComboBoxItem item in ThemeCombo.Items)
                {
                    if (item.Content.ToString() == _currentUser.SelectedTheme)
                    {
                        ThemeCombo.SelectedItem = item;
                        break;
                    }
                }
            }

            // Set Wallpaper status
            if (!string.IsNullOrEmpty(_currentUser.WallpaperPath) && File.Exists(_currentUser.WallpaperPath))
            {
                _wallpaperPath = _currentUser.WallpaperPath;
                WallpaperStatus.Text = "Wallpaper: " + Path.GetFileName(_currentUser.WallpaperPath);
            }
            else
            {
                WallpaperStatus.Text = "No custom wallpaper set.";
            }

            _profileImagePath = _currentUser.ProfilePicture;
            if (!string.IsNullOrEmpty(_profileImagePath) && File.Exists(_profileImagePath))
            {
                try
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(_profileImagePath, UriKind.Absolute);
                    bmp.EndInit();
                    ProfileImagePreview.Source = bmp;
                    ProfileImagePreview.Visibility = Visibility.Visible;
                    ProfileInitials.Visibility = Visibility.Collapsed;
                }
                catch { }
            }
        }

        private string _wallpaperPath = null;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string selectedTheme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Content.ToString();

            // Update profile information
            bool profileUpdated = _authService.UpdateProfile(
                _currentUser.Id,
                FullNameBox.Text.Trim(),
                EmailBox.Text.Trim(),
                PhoneBox.Text.Trim(),
                PlaceBox.Text.Trim(),
                EducationBox.Text.Trim(),
                EmergencyBox.Text.Trim(),
                DepartmentBox.Text.Trim(),
                DesignationBox.Text.Trim(),
                _profileImagePath,
                _wallpaperPath,
                selectedTheme);

            // Handle password change if provided
            bool passwordChanged = false;
            string currentPwd = CurrentPasswordBox.Password;
            string newPwd = NewPasswordBox.Password;
            string confirmPwd = ConfirmPasswordBox.Password;

            if (!string.IsNullOrEmpty(currentPwd) || !string.IsNullOrEmpty(newPwd) || !string.IsNullOrEmpty(confirmPwd))
            {
                if (string.IsNullOrEmpty(currentPwd))
                {
                    ErrorText.Text = "Please enter your current password to change password.";
                    return;
                }
                if (string.IsNullOrEmpty(newPwd))
                {
                    ErrorText.Text = "Please enter a new password.";
                    return;
                }
                if (newPwd.Length < 6)
                {
                    ErrorText.Text = "New password must be at least 6 characters.";
                    return;
                }
                if (newPwd != confirmPwd)
                {
                    ErrorText.Text = "New password and confirmation do not match.";
                    return;
                }

                passwordChanged = _authService.ChangePassword(_currentUser.Id, currentPwd, newPwd);
                if (!passwordChanged)
                {
                    ErrorText.Text = "Current password is incorrect.";
                    return;
                }
            }

            if (profileUpdated || passwordChanged)
            {
                // Refresh local session user data
                using (var conn = DatabaseService.GetConnection())
                {
                    var updatedUser = conn.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Id=@Id", new { Id = _currentUser.Id });
                    if (updatedUser != null) SessionManager.Login(updatedUser);
                }

                MessageBox.Show("Profile updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                ErrorText.Text = "No changes to save.";
            }
        }

        private void UploadPicture_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select Profile Picture"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string uploadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnterpriseWorkReport", "Uploads", "Images");
                    Directory.CreateDirectory(uploadsFolder);
                    
                    string ext = Path.GetExtension(dlg.FileName);
                    string targetPath = Path.Combine(uploadsFolder, $"user_{_currentUser.Id}_profile_{DateTime.Now.Ticks}{ext}");
                    File.Copy(dlg.FileName, targetPath, true);
                    
                    _profileImagePath = targetPath;
                    
                    // Show in preview
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(targetPath, UriKind.Absolute);
                    bmp.EndInit();
                    ProfileImagePreview.Source = bmp;
                    ProfileImagePreview.Visibility = Visibility.Visible;
                    ProfileInitials.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to upload picture: {ex.Message}", "Error");
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SelectFromGallery_Click(object sender, RoutedEventArgs e)
        {
            var galleryWindow = new Window
            {
                Title = "Select Profile Picture",
                Width = 500,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };

            var imagesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
            var imageFiles = Directory.Exists(imagesFolder) 
                ? Directory.GetFiles(imagesFolder, "*.jpg").Concat(Directory.GetFiles(imagesFolder, "*.jpeg")).Concat(Directory.GetFiles(imagesFolder, "*.png")).ToList()
                : new List<string>();

            if (!imageFiles.Any())
            {
                MessageBox.Show("No images found in the Images folder.", "Gallery Empty", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var wrapPanel = new WrapPanel { Margin = new Thickness(15) };
            foreach (var imageFile in imageFiles)
            {
                var border = new Border
                {
                    Width = 90,
                    Height = 90,
                    Margin = new Thickness(5),
                    BorderThickness = new Thickness(2),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                    Cursor = Cursors.Hand,
                    Tag = imageFile,
                    Child = new Image
                    {
                        Stretch = Stretch.UniformToFill,
                        Source = new BitmapImage(new Uri(imageFile))
                    }
                };

                border.MouseLeftButtonDown += (s, args) =>
                {
                    var selectedPath = s is Border b && b.Tag is string path ? path : null;
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        _profileImagePath = selectedPath;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(selectedPath, UriKind.Absolute);
                        bmp.EndInit();
                        ProfileImagePreview.Source = bmp;
                        ProfileImagePreview.Visibility = Visibility.Visible;
                        ProfileInitials.Visibility = Visibility.Collapsed;
                        galleryWindow.Close();
                    }
                };

                border.MouseEnter += (s, args) =>
                {
                    if (s is Border b) b.BorderBrush = (SolidColorBrush)FindResource("PrimaryBrush");
                };

                border.MouseLeave += (s, args) =>
                {
                    if (s is Border b) b.BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));
                };

                wrapPanel.Children.Add(border);
            }

            galleryWindow.Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = wrapPanel
            };

            galleryWindow.ShowDialog();
        }

        private void ChooseWallpaper_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select Dashboard Wallpaper"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string wallpaperDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnterpriseWorkReport", "Uploads", "Wallpapers");
                    Directory.CreateDirectory(wallpaperDir);
                    
                    string ext = Path.GetExtension(dlg.FileName);
                    string targetPath = Path.Combine(wallpaperDir, $"user_{_currentUser.Id}_wallpaper_{DateTime.Now.Ticks}{ext}");
                    File.Copy(dlg.FileName, targetPath, true);
                    
                    _wallpaperPath = targetPath;
                    WallpaperStatus.Text = "Wallpaper: " + Path.GetFileName(targetPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to set wallpaper: {ex.Message}", "Error");
                }
            }
        }

        private void ClearWallpaper_Click(object sender, RoutedEventArgs e)
        {
            _wallpaperPath = null;
            WallpaperStatus.Text = "No custom wallpaper set.";
        }
    }
}