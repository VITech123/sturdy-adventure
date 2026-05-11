using System;
using System.Windows;
using System.Windows.Input;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class PasswordDialog : Window
    {
        public string Password { get; private set; }

        public PasswordDialog()
        {
            InitializeComponent();
            PasswordBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Password = PasswordBox.Password;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Password = PasswordBox.Password;
                DialogResult = true;
                Close();
            }
        }
    }
}
