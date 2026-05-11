using System.Windows;
using Dapper;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ForgotPasswordDialog : Window
    {
        public ForgotPasswordDialog()
        {
            InitializeComponent();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageText.Text = "Please enter a username.";
                MessageText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            // Check if user exists
            using (var conn = DatabaseService.GetConnection())
            {
                var user = conn.QueryFirstOrDefault<Models.User>(
                    "SELECT * FROM Users WHERE Username = @Username AND IsActive = 1",
                    new { Username = username });

                if (user == null)
                {
                    MessageText.Text = "User not found or inactive.";
                    MessageText.Foreground = System.Windows.Media.Brushes.Red;
                    return;
                }

                // Check if the current logged in user is admin (if any)
                var currentUser = SessionManager.CurrentUser;
                if (currentUser != null && currentUser.Role == "Administrator")
                {
                    // Admin can directly reset password
                    var newPassword = NewPasswordBox.Password;
                    if (string.IsNullOrWhiteSpace(newPassword))
                    {
                        MessageText.Text = "Please enter a new password.";
                        MessageText.Foreground = System.Windows.Media.Brushes.Red;
                        return;
                    }

                    // Update password
                    var hashedPassword = AuthService.HashPassword(newPassword);
                    conn.Execute("UPDATE Users SET PasswordHash = @Password WHERE Id = @Id",
                        new { Password = hashedPassword, Id = user.Id });

                    AuditService.Log("Password Reset", $"Admin reset password for user: {username}");
                    MessageText.Text = $"Password has been reset for user: {user.FullName}";
                    MessageText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    // Regular user - show instructions
                    MessageText.Text = $"Please contact your administrator to reset the password for: {user.FullName}\n\nYour registered email: {user.Email ?? "Not set"}";
                    MessageText.Foreground = System.Windows.Media.Brushes.Blue;
                    AuditService.Log("Password Reset Request", $"Password reset requested for user: {username}");
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
