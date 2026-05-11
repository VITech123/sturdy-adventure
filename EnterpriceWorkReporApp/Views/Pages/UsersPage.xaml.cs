using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class UsersPage : Page
    {
        private List<User> _allUsers = new();

        public UsersPage()
        {
            InitializeComponent();
            
            // Security check: Only admins can manage users
            if (!SessionManager.IsAdmin)
            {
                MessageBox.Show("Access Denied. Only administrators can manage users.", 
                    "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Warning);
                NavigationService?.GoBack();
                return;
            }
            
            // Set default selection after InitializeComponent to avoid XAML initialization events
            if (FilterRole.Items.Count > 0)
                FilterRole.SelectedIndex = 0;
            if (FilterStatus.Items.Count > 0)
                FilterStatus.SelectedIndex = 0;
            
            LoadUsers();
        }

        private void LoadUsers()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var users = conn.Query<User>("SELECT * FROM Users ORDER BY FullName").ToList();
                _allUsers = users;
                ApplyFilters();
            }
        }

        private void ApplyFilters()
        {
            // Guard against null controls during XAML initialization
            if (UsersGrid == null || UsersGallery == null || FilterRole == null || FilterStatus == null || SearchBox == null) return;
            
            var filtered = _allUsers.AsEnumerable();

            // Role filter
            if (FilterRole.SelectedIndex > 0)
            {
                string role = (FilterRole.SelectedItem as ComboBoxItem)?.Content.ToString();
                filtered = filtered.Where(u => u.Role == role);
            }

            // Status filter
            if (FilterStatus.SelectedIndex > 0)
            {
                bool active = (FilterStatus.SelectedItem as ComboBoxItem)?.Content.ToString() == "Active";
                filtered = filtered.Where(u => u.IsActive == active);
            }

            // Search
            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                string search = SearchBox.Text.ToLower();
                filtered = filtered.Where(u => 
                    u.FullName.ToLower().Contains(search) || 
                    u.Username.ToLower().Contains(search) ||
                    (u.Email != null && u.Email.ToLower().Contains(search)));
            }

            var result = filtered.ToList();
            UsersGrid.ItemsSource = result;
            UsersGallery.ItemsSource = result;
        }

        private void ViewMode_Changed(object sender, RoutedEventArgs e)
        {
            if (UsersGrid == null || UsersGallery == null) return;

            if (ListToggle.IsChecked == true)
            {
                UsersGrid.Visibility = Visibility.Visible;
                UsersGallery.Visibility = Visibility.Collapsed;
            }
            else
            {
                UsersGrid.Visibility = Visibility.Collapsed;
                UsersGallery.Visibility = Visibility.Visible;
            }
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new UserDialog { Owner = mainWindow };
            if (dialog.ShowDialog() == true) LoadUsers();
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var user = _allUsers.FirstOrDefault(u => u.Id == id);
                if (user != null)
                {
                    var mainWindow = Window.GetWindow(this);
                    var dialog = new UserDialog(user) { Owner = mainWindow };
                    if (dialog.ShowDialog() == true) LoadUsers();
                }
            }
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var user = _allUsers.FirstOrDefault(u => u.Id == id);
                string newPwd = "vendhan@123";
                string hashedPwd = AuthService.HashPassword(newPwd);
                using (var conn = DatabaseService.GetConnection())
                    conn.Execute("UPDATE Users SET PasswordHash=@P WHERE Id=@Id", new { P = hashedPwd, Id = id });
                AuditService.Log("Password Reset", $"Admin reset password for user: {user?.FullName}");
                MessageBox.Show($"Password for {user?.FullName} has been reset to: {newPwd}", "Reset Password", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeactivateUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var user = _allUsers.FirstOrDefault(u => u.Id == id);
                if (MessageBox.Show($"Deactivate user '{user?.FullName}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = DatabaseService.GetConnection())
                        conn.Execute("UPDATE Users SET IsActive=0 WHERE Id=@Id", new { Id = id });
                    LoadUsers();
                }
            }
        }

        private void ViewUserDetails_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is User user)
            {
                var mainWindow = Window.GetWindow(this);
                var dialog = new UserDetailDialog(user) { Owner = mainWindow };
                dialog.ShowDialog();
            }
        }

        private void UsersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (UsersGrid.SelectedItem is User user)
            {
                var mainWindow = Window.GetWindow(this);
                var dialog = new UserDetailDialog(user) { Owner = mainWindow };
                dialog.ShowDialog();
            }
        }
    }
}
