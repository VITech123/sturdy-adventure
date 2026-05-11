using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public class CalendarLeaveBadge
    {
        public string Label { get; set; }
        public string Tooltip { get; set; }
        public Brush BadgeColor { get; set; }
    }

    public class CalendarDay
    {
        public string DayNumber { get; set; }
        public string DayFontWeight { get; set; } = "Normal";
        public Brush DayForeground { get; set; } = Brushes.Black;
        public Brush CellBackground { get; set; } = Brushes.White;
        public ObservableCollection<CalendarLeaveBadge> Leaves { get; set; } = new ObservableCollection<CalendarLeaveBadge>();
    }

    public partial class LeaveCalendarDialog : Window
    {
        private DateTime _currentDate;

        public LeaveCalendarDialog()
        {
            InitializeComponent();
            InitializeSelectors();
            _currentDate = DateTime.Today;
            UpdateSelectors(_currentDate);
            LoadCalendar();
        }

        private void InitializeSelectors()
        {
            MonthSelector.Items.Clear();
            var months = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.MonthNames;
            for (int i = 0; i < 12; i++)
            {
                MonthSelector.Items.Add(months[i]);
            }

            YearSelector.Items.Clear();
            int currentYear = DateTime.Now.Year;
            for (int i = currentYear - 5; i <= currentYear + 5; i++)
            {
                YearSelector.Items.Add(i.ToString());
            }
        }

        private void UpdateSelectors(DateTime date)
        {
            MonthSelector.SelectedIndex = date.Month - 1;
            YearSelector.SelectedItem = date.Year.ToString();
        }

        private void LoadCalendar()
        {
            if (MonthSelector.SelectedIndex == -1 || YearSelector.SelectedIndex == -1) return;

            int month = MonthSelector.SelectedIndex + 1;
            int year = int.Parse(YearSelector.SelectedItem.ToString());
            DateTime monthStart = new DateTime(year, month, 1);
            DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);

            List<LeaveRequest> allLeaves;
            using (var conn = DatabaseService.GetConnection())
            {
                string query = @"
                    SELECT l.*, u.FullName AS EmployeeName
                    FROM Leaves l
                    JOIN Users u ON u.Id = l.UserId
                    WHERE (l.StartDate <= @End AND l.EndDate >= @Start)
                    AND l.Status IN ('Approved', 'Pending')";
                
                allLeaves = conn.Query<LeaveRequest>(query, new { Start = monthStart, End = monthEnd }).AsList();
            }

            var days = new ObservableCollection<CalendarDay>();

            int daysInMonth = DateTime.DaysInMonth(year, month);
            
            // Monday = 1, Sunday = 7 (ISO 8601 uses 1 for Monday. DayOfWeek uses 0 for Sunday)
            int currentDayOfWeek = (int)monthStart.DayOfWeek;
            int offset = currentDayOfWeek - 1;
            if (offset < 0) offset = 6; // Sunday is 7th day

            var approvedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C6F6D5")); // Light green
            var pendingBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEFCBF")); // Light yellow
            var dimGray = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")); // Gray for inactive days

            // Previous month trailing days
            DateTime prevMonth = monthStart.AddMonths(-1);
            int prevMonthDays = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
            for (int i = offset - 1; i >= 0; i--)
            {
                days.Add(new CalendarDay
                {
                    DayNumber = (prevMonthDays - i).ToString(),
                    DayForeground = dimGray,
                    CellBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB")) // Light gray
                });
            }

            // Current month days
            long todayTicks = DateTime.Today.Ticks;
            for (int i = 1; i <= daysInMonth; i++)
            {
                DateTime dt = new DateTime(year, month, i);
                var day = new CalendarDay
                {
                    DayNumber = i.ToString(),
                    DayFontWeight = dt.Ticks == todayTicks ? "Bold" : "Normal",
                    DayForeground = dt.Ticks == todayTicks ? (SolidColorBrush)FindResource("PrimaryBrush") : Brushes.Black,
                    CellBackground = Brushes.White
                };

                // Find leaves for this day
                var todaysLeaves = allLeaves.Where(l => l.StartDate.Date <= dt && l.EndDate.Date >= dt).ToList();
                foreach (var leave in todaysLeaves)
                {
                    day.Leaves.Add(new CalendarLeaveBadge
                    {
                        Label = $"{leave.EmployeeName}",
                        Tooltip = $"{leave.EmployeeName}\nReason: {leave.Reason}\nType: {leave.LeaveType}\nStatus: {leave.Status}",
                        BadgeColor = leave.Status == "Approved" ? approvedBrush : pendingBrush
                    });
                }
                
                days.Add(day);
            }

            // Next month leading days to fill 42 cells (6 rows x 7 cols)
            int remaining = 42 - days.Count;
            for (int i = 1; i <= remaining; i++)
            {
                days.Add(new CalendarDay
                {
                    DayNumber = i.ToString(),
                    DayForeground = dimGray,
                    CellBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB"))
                });
            }

            CalendarItemsControl.ItemsSource = days;
        }

        private void Period_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (MonthSelector.SelectedIndex != -1 && YearSelector.SelectedIndex != -1)
            {
                int month = MonthSelector.SelectedIndex + 1;
                int year = int.Parse(YearSelector.SelectedItem.ToString());
                _currentDate = new DateTime(year, month, 1);
                LoadCalendar();
            }
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentDate.AddMonths(-1);
            UpdateSelectors(_currentDate);
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentDate.AddMonths(1);
            UpdateSelectors(_currentDate);
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = DateTime.Today;
            UpdateSelectors(_currentDate);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}