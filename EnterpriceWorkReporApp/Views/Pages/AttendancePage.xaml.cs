using System;
using System.Collections.Generic;
using Npgsql;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;
using Microsoft.Win32;
using CompanySettings = EnterpriseWorkReport.Models.CompanySettings;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class AttendancePage : Page
    {
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalCount = 0;
        private List<Attendance> _allRecords = new();
        private string _lateThreshold = "09:30";
        private string _earlyThreshold = "18:00";

        public AttendancePage()
        {
            try
            {
                InitializeComponent();
                
                if (FilterDateFrom != null) FilterDateFrom.SelectedDate = DateTime.Today.AddDays(-30);
                if (FilterDateTo != null) FilterDateTo.SelectedDate = DateTime.Today;
                
                if (FilterStatus != null && FilterStatus.Items.Count > 0)
                    FilterStatus.SelectedIndex = 0;

                if (PageSizeCombo != null)
                    PageSizeCombo.SelectedIndex = 1; // Default 50
                    
                if (!SessionManager.IsAdmin)
                {
                    if (EmployeeCol != null) EmployeeCol.Visibility = Visibility.Collapsed;
                    if (ActionsCol != null) ActionsCol.Visibility = Visibility.Collapsed;
                    if (FilterEmployeeName != null) FilterEmployeeName.Visibility = Visibility.Collapsed;
                }

                LoadData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AttendancePage Init Error: {ex}");
            }
        }

        private void LoadData()
        {
            if (AttendanceGrid == null) return;

            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    // Load late/early thresholds from settings
                    var settings = conn.QueryFirstOrDefault<CompanySettings>(
                        "SELECT LateArrivalThreshold, EarlyDepartureThreshold FROM CompanySettings WHERE Id = 1");
                    if (settings != null)
                    {
                        if (!string.IsNullOrEmpty(settings.LateArrivalThreshold))
                            _lateThreshold = settings.LateArrivalThreshold;
                        if (!string.IsNullOrEmpty(settings.EarlyDepartureThreshold))
                            _earlyThreshold = settings.EarlyDepartureThreshold;
                    }

                    var whereClauses = new List<string>();
                    var parameters = new DynamicParameters();
                    
                    parameters.Add("Limit", _pageSize);
                    parameters.Add("Offset", (_currentPage - 1) * _pageSize);

                    if (!SessionManager.IsAdmin)
                    {
                        whereClauses.Add("a.UserId = @UserId");
                        parameters.Add("UserId", SessionManager.CurrentUser.Id);
                    }

                    if (!string.IsNullOrWhiteSpace(FilterEmployeeName?.Text) && SessionManager.IsAdmin)
                    {
                        whereClauses.Add("u.FullName ILIKE @Emp");
                        parameters.Add("Emp", $"%{FilterEmployeeName.Text.Trim()}%");
                    }

                    if (FilterDateFrom.SelectedDate.HasValue)
                    {
                        whereClauses.Add("a.Date >= @From");
                        parameters.Add("From", FilterDateFrom.SelectedDate.Value.Date);
                    }

                    if (FilterDateTo.SelectedDate.HasValue)
                    {
                        whereClauses.Add("a.Date <= @To");
                        parameters.Add("To", FilterDateTo.SelectedDate.Value.Date);
                    }

                    if (FilterStatus != null && FilterStatus.SelectedItem is ComboBoxItem si)
                    {
                        string sel = si.Content?.ToString() ?? "";
                        if (sel != "All Statuses" && !string.IsNullOrEmpty(sel))
                        {
                            whereClauses.Add("a.Status = @Status");
                            parameters.Add("Status", sel);
                        }
                    }

                    string where = whereClauses.Any() ? "WHERE " + string.Join(" AND ", whereClauses) : "";

                    string sql = $@"
                        SELECT a.*, u.FullName AS EmployeeName
                        FROM Attendance a
                        JOIN Users u ON u.Id = a.UserId
                        {where}
                        ORDER BY a.Date DESC, u.FullName
                        LIMIT @Limit OFFSET @Offset";

                    string countSql = $@"
                        SELECT COUNT(*) 
                        FROM Attendance a
                        JOIN Users u ON u.Id = a.UserId
                        {where}";

                    _allRecords = conn.Query<Attendance>(sql, parameters).AsList();
                    _totalCount = (int)conn.ExecuteScalar<long>(countSql, parameters);

                    // Post-process to calculate late/early flags (since they depend on Thresholds)
                    foreach (var rec in _allRecords)
                    {
                        if (!string.IsNullOrEmpty(rec.ClockInTime))
                        {
                            if (TimeSpan.TryParse(rec.ClockInTime, out var clockIn) && TimeSpan.TryParse(_lateThreshold, out var late))
                                rec.IsLateArrival = clockIn > late;
                        }
                        if (!string.IsNullOrEmpty(rec.ClockOutTime))
                        {
                            if (TimeSpan.TryParse(rec.ClockOutTime, out var clockOut) && TimeSpan.TryParse(_earlyThreshold, out var early))
                                rec.IsEarlyDeparture = clockOut < early;
                        }
                    }

                    AttendanceGrid.ItemsSource = _allRecords;
                    UpdatePaginationUI();
                    UpdateClockButtons();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading attendance data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePaginationUI()
        {
            if (AttendanceGrid == null || PageInfoText == null || PrevPageBtn == null || NextPageBtn == null) return;

            int totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
            if (totalPages == 0) totalPages = 1;

            PageInfoText.Text = $"Page {_currentPage} of {totalPages} (Total: {_totalCount})";
            PrevPageBtn.IsEnabled = _currentPage > 1;
            NextPageBtn.IsEnabled = _currentPage < totalPages;
        }

        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (PageSizeCombo == null || !IsLoaded) return;
            if (PageSizeCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString(), out int size))
            {
                _pageSize = size;
                _currentPage = 1;
                LoadData();
            }
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                LoadData();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
            if (_currentPage < totalPages)
            {
                _currentPage++;
                LoadData();
            }
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (FilterEmployeeName == null || FilterDateFrom == null || FilterDateTo == null || FilterStatus == null) return;
            _currentPage = 1;
            LoadData();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterDateFrom.SelectedDate = null;
            FilterDateTo.SelectedDate = null;
            FilterEmployeeName.Text = "";
            FilterStatus.SelectedIndex = 0;
            _currentPage = 1;
            LoadData();
        }

        private void UpdateClockButtons()
        {
            if (ClockInBtn == null || ClockOutBtn == null) return;
            var today = DateTime.Today;
            var userId = SessionManager.CurrentUser?.Id ?? 0;
            if (userId == 0)
            {
                ClockInBtn.IsEnabled = false;
                ClockOutBtn.IsEnabled = false;
                return;
            }

            // We check the DB for today's record specifically for the current user
            using (var conn = DatabaseService.GetConnection())
            {
                var todayRecord = conn.QueryFirstOrDefault<Attendance>(
                    "SELECT * FROM Attendance WHERE UserId = @UserId AND Date = @Date",
                    new { UserId = userId, Date = today });

                if (todayRecord != null)
                {
                    ClockInBtn.IsEnabled = string.IsNullOrEmpty(todayRecord.ClockInTime);
                    ClockOutBtn.IsEnabled = !string.IsNullOrEmpty(todayRecord.ClockInTime) &&
                                            string.IsNullOrEmpty(todayRecord.ClockOutTime);
                }
                else
                {
                    ClockInBtn.IsEnabled = true;
                    ClockOutBtn.IsEnabled = false;
                }
            }
        }

        private void ClockIn_Click(object sender, RoutedEventArgs e)
        {
            RecordClock(true);
        }

        private void ClockOut_Click(object sender, RoutedEventArgs e)
        {
            RecordClock(false);
        }

        private void RecordClock(bool isClockIn)
        {
            var userId = SessionManager.CurrentUser?.Id;
            if (userId == null)
            {
                MessageBox.Show("Please log in to record attendance.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var today = DateTime.Today;
            var now = DateTime.Now;
            string timeStr = now.ToString("HH:mm");

            using (var conn = DatabaseService.GetConnection())
            {
                var existing = conn.QueryFirstOrDefault<Attendance>(
                    "SELECT * FROM Attendance WHERE UserId = @UserId AND Date = @Date",
                    new { UserId = userId, Date = today });

                if (existing == null)
                {
                    if (!isClockIn)
                    {
                        MessageBox.Show("You must clock in before clocking out.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    conn.Execute(@"
                        INSERT INTO Attendance (UserId, Date, Status, ClockInTime, ClockOutTime, Remarks)
                        VALUES (@UserId, @Date, 'Present', @ClockIn, '', '')",
                        new { UserId = userId, Date = today, ClockIn = timeStr });
                    NotificationService.Show("Clock In", $"Successfully clocked in at {timeStr}.");
                }
                else
                {
                    if (isClockIn && string.IsNullOrEmpty(existing.ClockInTime))
                    {
                        conn.Execute("UPDATE Attendance SET ClockInTime = @Time WHERE Id = @Id",
                            new { Time = timeStr, Id = existing.Id });
                        NotificationService.Show("Clock In", $"Successfully clocked in at {timeStr}.");
                    }
                    else if (!isClockIn && !string.IsNullOrEmpty(existing.ClockInTime) && string.IsNullOrEmpty(existing.ClockOutTime))
                    {
                        double hoursWorked = 0;
                        if (DateTime.TryParse(existing.ClockInTime, out DateTime inTime))
                        {
                            DateTime outTime = now;
                            if (outTime < inTime) outTime = outTime.AddDays(1);
                            hoursWorked = Math.Round((outTime - inTime).TotalHours, 2);
                        }

                        conn.Execute("UPDATE Attendance SET ClockOutTime = @Time, HoursWorked = @H WHERE Id = @Id",
                            new { Time = timeStr, H = hoursWorked, Id = existing.Id });
                        NotificationService.Show("Clock Out", $"Successfully clocked out at {timeStr}. Hours: {hoursWorked:F1}h");
                    }
                    else
                    {
                        MessageBox.Show(isClockIn ? "Already clocked in today." : "Already clocked out today.",
                            "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            LoadData();
        }

        private void MarkAttendance_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new AttendanceDialog { Owner = mainWindow };
            if (dialog.ShowDialog() == true) LoadData();
        }

        private void EditAttendance_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var rec = _allRecords.FirstOrDefault(a => a.Id == id);
                if (rec != null)
                {
                    var mainWindow = Window.GetWindow(this);
                    var dialog = new AttendanceDialog(rec) { Owner = mainWindow };
                    if (dialog.ShowDialog() == true) LoadData();
                }
            }
        }

        private void DeleteAttendance_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                if (MessageBox.Show("Delete this attendance record?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        conn.Execute("DELETE FROM Attendance WHERE Id=@Id", new { Id = id });
                    }
                    LoadData();
                }
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "CSV Files|*.csv", FileName = $"Attendance_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;
            var rows = AttendanceGrid.ItemsSource as IEnumerable<Attendance> ?? _allRecords;
            using (var sw = new StreamWriter(dlg.FileName))
            {
                sw.WriteLine("Employee,Date,Status,Remarks,ClockIn,ClockOut,Hours");
                foreach (var r in rows)
                    sw.WriteLine($"{r.EmployeeName},{r.Date:dd/MM/yyyy},{r.Status},{r.Remarks},{r.ClockInTime},{r.ClockOutTime},{r.HoursWorked:F1}");
            }
            MessageBox.Show("Exported.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
