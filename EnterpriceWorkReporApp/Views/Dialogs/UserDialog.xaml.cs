using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class UserDialog : Window
    {
        private readonly User _user;
        private readonly bool _isEdit;

        public UserDialog(User existing = null)
        {
            InitializeComponent();
            _user = existing ?? new User();
            _isEdit = existing != null;

            if (_isEdit)
            {
                TitleText.Text = "Edit User";
                FullNameBox.Text = _user.FullName;
                UsernameBox.Text = _user.Username;
                UsernameBox.IsReadOnly = true;
                PasswordBox.Password = ""; // Don't show password

                EmailBox.Text = _user.Email;
                PhoneBox.Text = _user.Phone;
                PlaceBox.Text = _user.Place;
                EducationBox.Text = _user.Education;
                EmergencyBox.Text = _user.EmergencyNumber;
                JoinDatePicker.SelectedDate = _user.JoinDate;
                RelievingDatePicker.SelectedDate = _user.RelievingDate;

                if (!string.IsNullOrEmpty(_user.ProfilePicture) && System.IO.File.Exists(_user.ProfilePicture))
                {
                    try { ProfilePhotoPreview.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(_user.ProfilePicture)); } catch { }
                }

                foreach (ComboBoxItem item in RoleCombo.Items)
                    if (item.Content.ToString() == _user.Role)
                    { RoleCombo.SelectedItem = item; break; }
            }
        }

        private string _selectedPhotoPath = null;
        private void UploadPhoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select Profile Photo"
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedPhotoPath = dialog.FileName;
                ProfilePhotoPreview.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(_selectedPhotoPath));
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FullNameBox.Text)) { ErrorText.Text = "Full name required."; return; }
            if (string.IsNullOrWhiteSpace(UsernameBox.Text)) { ErrorText.Text = "Username required."; return; }
            if (!_isEdit && string.IsNullOrWhiteSpace(PasswordBox.Password)) { ErrorText.Text = "Password required."; return; }

            string role = (RoleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Employee";
            string rawPassword = !string.IsNullOrWhiteSpace(PasswordBox.Password) ? PasswordBox.Password : (_isEdit ? null : "vendhan@123");
            
            // Handle Profile Photo storage
            string finalPhotoPath = _user.ProfilePicture;
            if (!string.IsNullOrEmpty(_selectedPhotoPath))
            {
                try
                {
                    string profileDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data", "profiles");
                    if (!System.IO.Directory.Exists(profileDir)) System.IO.Directory.CreateDirectory(profileDir);
                    
                    string ext = System.IO.Path.GetExtension(_selectedPhotoPath);
                    string fileName = $"{UsernameBox.Text.Trim()}_{DateTime.Now.Ticks}{ext}";
                    finalPhotoPath = System.IO.Path.Combine(profileDir, fileName);
                    System.IO.File.Copy(_selectedPhotoPath, finalPhotoPath, true);
                }
                catch (Exception ex) { Debug.WriteLine("Photo save error: " + ex.Message); }
            }

            using (var conn = DatabaseService.GetConnection())
            {
                var parameters = new DynamicParameters();
                parameters.Add("FN", FullNameBox.Text.Trim());
                parameters.Add("R", role);
                parameters.Add("E", EmailBox.Text.Trim());
                parameters.Add("P", PhoneBox.Text.Trim());
                parameters.Add("Place", PlaceBox.Text.Trim());
                parameters.Add("Edu", EducationBox.Text.Trim());
                parameters.Add("Emer", EmergencyBox.Text.Trim());
                parameters.Add("JD", JoinDatePicker.SelectedDate);
                parameters.Add("RD", RelievingDatePicker.SelectedDate);
                parameters.Add("Photo", finalPhotoPath);

                if (_isEdit)
                {
                    parameters.Add("Id", _user.Id);
                    string passwordUpdate = "";
                    if (!string.IsNullOrWhiteSpace(rawPassword))
                    {
                        passwordUpdate = ", PasswordHash=@PWD";
                        parameters.Add("PWD", AuthService.HashPassword(rawPassword));
                    }

                    conn.Execute($@"UPDATE Users SET 
                        FullName=@FN, Role=@R, Email=@E, Phone=@P, Place=@Place, 
                        Education=@Edu, EmergencyNumber=@Emer, JoinDate=@JD, 
                        RelievingDate=@RD, ProfilePicture=@Photo{passwordUpdate} 
                        WHERE Id=@Id", parameters);
                    
                    AuditService.Log("User Updated", $"User ID {_user.Id}: {FullNameBox.Text}");
                }
                else
                {
                    long existing = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users WHERE Username=@U", new { U = UsernameBox.Text.Trim() });
                    if (existing > 0) { ErrorText.Text = "Username already exists."; return; }

                    parameters.Add("U", UsernameBox.Text.Trim());
                    parameters.Add("PWD", AuthService.HashPassword(rawPassword));

                    conn.Execute(@"INSERT INTO Users 
                        (Username, FullName, PasswordHash, Role, IsActive, Email, Phone, Place, Education, EmergencyNumber, JoinDate, RelievingDate, ProfilePicture) 
                        VALUES (@U, @FN, @PWD, @R, 1, @E, @P, @Place, @Edu, @Emer, @JD, @RD, @Photo)", parameters);
                    
                    AuditService.Log("User Created", $"New user: {UsernameBox.Text}");
                }
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private void GeneratePassword_Click(object sender, RoutedEventArgs e)
        {
            // Generate a memorable password pattern: FirstName123!
            string firstName = FullNameBox.Text.Trim();
            if (string.IsNullOrEmpty(firstName))
            {
                GeneratedPasswordText.Text = "Enter full name first";
                GeneratedPasswordText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }
            
            // Get first name (first word)
            string baseName = firstName.Split(' ')[0];
            baseName = baseName.ToLower();
            
            // Generate pattern: FirstName + year + special char
            string year = DateTime.Now.Year.ToString().Substring(2); // Last 2 digits
            string[] special = { "!", "@", "#", "$", "%", "&" };
            Random rnd = new Random();
            string specialChar = special[rnd.Next(special.Length)];
            
            // Create memorable password: John@123
            string password = $"{baseName}{specialChar}{rnd.Next(10, 99)}";
            
            PasswordBox.Password = password;
            GeneratedPasswordText.Text = $"Generated: {password}";
            GeneratedPasswordText.Foreground = System.Windows.Media.Brushes.Green;
        }
    }
}
