using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EnterpriseWorkReport.Services
{
    public enum NotificationType { Success, Error, Warning, Info }

    public class NotificationService
    {
        private static Window _activeWindow => Application.Current.MainWindow;
        private static Panel _notificationContainer;

        public static void Initialize(Panel container)
        {
            _notificationContainer = container;
        }

        public static void Show(string title, string message, NotificationType type = NotificationType.Info)
        {
            if (_notificationContainer == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var toast = CreateToast(title, message, type);
                _notificationContainer.Children.Add(toast);

                // Animate Slide In
                var slideAnimation = new ThicknessAnimation
                {
                    From = new Thickness(0, -50, 0, 50),
                    To = new Thickness(0, 0, 0, 10),
                    Duration = TimeSpan.FromMilliseconds(400),
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };

                toast.BeginAnimation(FrameworkElement.MarginProperty, slideAnimation);

                // Auto Close after 5 seconds
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    RemoveToast(toast);
                };
                timer.Start();
            });
        }

        private static Border CreateToast(string title, string message, NotificationType type)
        {
            var brush = type switch
            {
                NotificationType.Success => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                NotificationType.Error => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                NotificationType.Warning => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                _ => new SolidColorBrush(Color.FromRgb(59, 130, 246))
            };

            var icon = type switch
            {
                NotificationType.Success => "✅",
                NotificationType.Error => "❌",
                NotificationType.Warning => "⚠️",
                _ => "ℹ️"
            };

            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 10),
                MinWidth = 300,
                MaxWidth = 400,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 15,
                    Opacity = 0.1,
                    ShadowDepth = 5
                },
                BorderBrush = brush,
                BorderThickness = new Thickness(4, 0, 0, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(iconText, 0);
            grid.Children.Add(iconText);

            var stack = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 14 });
            stack.Children.Add(new TextBlock { Text = message, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)) });
            Grid.SetColumn(stack, 1);
            grid.Children.Add(stack);

            border.Child = grid;
            return border;
        }

        private static void RemoveToast(Border toast)
        {
            var fadeAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            fadeAnimation.Completed += (s, e) =>
            {
                _notificationContainer.Children.Remove(toast);
            };
            toast.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
        }
    }
}
