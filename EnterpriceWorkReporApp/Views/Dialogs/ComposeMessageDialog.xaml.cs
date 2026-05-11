using System.IO;
using System.Windows;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using Microsoft.Win32;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ComposeMessageDialog : Window
    {
        public ComposeMessageDialog()
        {
            InitializeComponent();
            LoadUsers();
        }

        private void LoadUsers()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var currentUserId = SessionManager.CurrentUser?.Id ?? 0;
                var users = conn.Query<User>("SELECT Id, FullName FROM Users WHERE Id != @CurrentUserId AND IsActive = 1 ORDER BY FullName",
                    new { CurrentUserId = currentUserId });
                ReceiverCombo.ItemsSource = users;
            }
        }

        private void BroadcastCheck_Changed(object sender, RoutedEventArgs e)
        {
            ReceiverCombo.IsEnabled = !BroadcastCheck.IsChecked == true;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Attachment",
                Filter = "All Files|*.*|Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Documents|*.pdf;*.doc;*.docx;*.xls;*.xlsx"
            };
            
            if (dlg.ShowDialog() == true)
            {
                AttachmentPath.Text = dlg.FileName;
                var fileInfo = new FileInfo(dlg.FileName);
                AttachmentInfo.Text = $"Selected: {fileInfo.Name} ({fileInfo.Length / 1024} KB)";
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ContentBox.Text))
            {
                ErrorText.Text = "Please enter a message.";
                return;
            }

            if (!BroadcastCheck.IsChecked == true && ReceiverCombo.SelectedValue == null)
            {
                ErrorText.Text = "Please select a recipient.";
                return;
            }

            var user = SessionManager.CurrentUser;
            if (user == null) return;

            var messageService = new MessageService();
            int? receiverId = BroadcastCheck.IsChecked == true ? (int?)null : (int?)ReceiverCombo.SelectedValue;
            bool isBroadcast = BroadcastCheck.IsChecked == true;

            messageService.SendMessage(
                user.Id,
                receiverId,
                SubjectBox.Text.Trim(),
                ContentBox.Text.Trim(),
                AttachmentPath.Text,
                isBroadcast
            );

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
