using System.Diagnostics;
using System.IO;
using System.Windows;
using EnterpriseWorkReport.Models;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ViewMessageDialog : Window
    {
        private readonly Message _message;

        public ViewMessageDialog(Message message)
        {
            InitializeComponent();
            _message = message;
            LoadMessage();
        }

        private void LoadMessage()
        {
            SubjectText.Text = string.IsNullOrWhiteSpace(_message.Subject) ? "(No Subject)" : _message.Subject;
            FromText.Text = _message.SenderName ?? "Unknown";
            ToText.Text = _message.ReceiverName ?? "All Users";
            DateText.Text = _message.CreatedAt.ToString("dddd, dd MMMM yyyy HH:mm");
            ContentText.Text = _message.Content;

            // Show attachment if present
            if (!string.IsNullOrEmpty(_message.AttachmentPath) && File.Exists(_message.AttachmentPath))
            {
                AttachmentPanel.Visibility = Visibility.Visible;
                AttachmentName.Text = Path.GetFileName(_message.AttachmentPath);
            }
            else if (!string.IsNullOrEmpty(_message.AttachmentPath))
            {
                AttachmentPanel.Visibility = Visibility.Visible;
                AttachmentName.Text = _message.AttachmentPath + " (File not found)";
            }
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_message.AttachmentPath) && File.Exists(_message.AttachmentPath))
            {
                try
                {
                    // Open the file with default application
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _message.AttachmentPath,
                        UseShellExecute = true
                    });
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Could not open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
