using System.Windows;
using System.Windows.Input;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;

namespace EnterpriseWorkReport.Views
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _authService = new AuthService();
        private bool _isPasswordVisible = false;

        public LoginWindow()
        {
            InitializeComponent();
            UsernameBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            AttemptLogin();
        }

        private void InputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                AttemptLogin();
        }

        private void AttemptLogin()
        {
            string username = UsernameBox.Text.Trim();
            string password = _isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter your username and password.");
                return;
            }

            LoginButton.IsEnabled = false;
            LoginButton.Content = "Signing in...";

            try
            {
                var user = _authService.Login(username, password);

                if (user == null)
                {
                    ShowError("Invalid username or password. Please try again.");
                    LoginButton.IsEnabled = true;
                    LoginButton.Content = "Sign In";
                    return;
                }

                SessionManager.Login(user);

                var mainWindow = new MainShellWindow();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                this.Close();
            }
            catch (System.Exception ex)
            {
                ShowError($"Login error: {ex.Message}");
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Sign In";
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (_isPasswordVisible)
            {
                // Hide password
                if (!string.IsNullOrEmpty(PasswordTextBox.Text))
                    PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                ShowPasswordBtn.Content = "👁";
                _isPasswordVisible = false;
            }
            else
            {
                // Show password
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                ShowPasswordBtn.Content = "🔒";
                _isPasswordVisible = true;
            }
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ForgotPasswordDialog { Owner = this };
            dialog.ShowDialog();
        }

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new HelpDialog { Owner = this };
            dialog.ShowDialog();
        }
    }
}
