using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class MessagesPage : Page
    {
        private readonly MessageService _messageService;
        private List<Message> _currentMessages;
        private string _currentView = "Inbox";

        public MessagesPage()
        {
            InitializeComponent();
            _messageService = new MessageService();
            LoadMessages();
        }

        private void LoadMessages()
        {
            var user = SessionManager.CurrentUser;
            if (user == null) return;

            if (_currentView == "Inbox")
            {
                _currentMessages = _messageService.GetInbox(user.Id);
            }
            else
            {
                _currentMessages = _messageService.GetSentMessages(user.Id);
            }

            MessagesGrid.ItemsSource = _currentMessages;
            EmptyText.Visibility = _currentMessages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Inbox_Click(object sender, RoutedEventArgs e)
        {
            _currentView = "Inbox";
            LoadMessages();
        }

        private void Sent_Click(object sender, RoutedEventArgs e)
        {
            _currentView = "Sent";
            LoadMessages();
        }

        private void Compose_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new ComposeMessageDialog { Owner = mainWindow };
            if (dialog.ShowDialog() == true)
            {
                LoadMessages();
            }
        }

        private void ViewMessage_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var message = _currentMessages.Find(m => m.Id == id);
                if (message != null)
                {
                    // Mark as read if inbox
                    if (_currentView == "Inbox" && !message.IsRead)
                    {
                        _messageService.MarkAsRead(id);
                    }

                    var mainWindow = Window.GetWindow(this);
                    var dialog = new ViewMessageDialog(message) { Owner = mainWindow };
                    dialog.ShowDialog();
                    LoadMessages();
                }
            }
        }

        private void DeleteMessage_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var confirm = MessageBox.Show(
                    "Are you sure you want to delete this message?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    _messageService.DeleteMessage(id);
                    LoadMessages();
                }
            }
        }

        private void MarkAllRead_Click(object sender, RoutedEventArgs e)
        {
            var user = SessionManager.CurrentUser;
            if (user != null)
            {
                _messageService.MarkAllAsRead(user.Id);
                LoadMessages();
            }
        }

        private void MessagesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    }
}
