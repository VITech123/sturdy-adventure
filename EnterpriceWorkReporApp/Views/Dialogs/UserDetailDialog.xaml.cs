using System;
using System.Collections.Generic;
using Npgsql;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class UserDetailDialog : Window
    {
        private readonly User _user;
        private readonly int _userId;

        // Extended models for display
        private class LeaveRequestDisplay
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string EmployeeName { get; set; }
            public string LeaveType { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string Reason { get; set; }
            public string Status { get; set; }
            public int Days => (EndDate - StartDate).Days + 1;
        }

        private class WorkReportDisplay
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public string ProjectName { get; set; }
            public string ObjectId { get; set; }
            public decimal BillingAmount { get; set; }
        }

        private class QualityReportDisplay
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public double QualityScore { get; set; }
            public double ErrorRate { get; set; }
            public int ReworkCount { get; set; }
            public string Remarks { get; set; }
        }

        private class ProjectDisplay
        {
            public string ProjectName { get; set; }
            public int ReportsCount { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime? LastReportDate { get; set; }
        }

        private List<WorkReportDisplay> _allWorkReports = new();
        private List<Attendance> _allAttendance = new();
        private List<QualityReportDisplay> _allQualityReports = new();
        private List<ProjectDisplay> _allProjects = new();
        private bool _isDataLoading = false;

        public UserDetailDialog(User user)
        {
            InitializeComponent();
            _user = user;
            _userId = user.Id;
            LoadUserData();
            LoadFilterOptions();
        }

        public UserDetailDialog(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadUserData();
            LoadFilterOptions();
        }

        private void LoadFilterOptions()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var projects = conn.Query<string>("SELECT Name FROM Projects ORDER BY Name").ToList();
                foreach (var project in projects)
                {
                    WorkFilterProject.Items.Add(new ComboBoxItem { Content = project });
                }
            }
        }

        private void LoadUserData()
        {
            _isDataLoading = true;
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    // Load user info
                    if (_user != null)
                    {
                        LoadUserInfo(_user);
                    }
                    else
                    {
                        var user = conn.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Id = @Id", new { Id = _userId });
                        if (user != null)
                            LoadUserInfo(user);
                    }

                    // Load attendance data
                    LoadAttendance(conn);

                    // Load leave data
                    LoadLeaveData(conn);

                    // Load work reports
                    LoadWorkReports(conn);

                    // Load quality reports
                    LoadQualityReports(conn);

                    // Load projects
                    LoadProjects(conn);
                }
            }
            finally
            {
                _isDataLoading = false;
            }
        }

        private void LoadUserInfo(User user)
        {
            // Set avatar initials
            string initials = "";
            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                var parts = user.FullName.Split(' ');
                foreach (var part in parts)
                {
                    if (!string.IsNullOrWhiteSpace(part))
                        initials += part[0].ToString().ToUpper();
                    if (initials.Length >= 2) break;
                }
            }
            AvatarText.Text = initials;

            // Handle Profile Picture
            if (!string.IsNullOrEmpty(user.ProfilePicture) && System.IO.File.Exists(user.ProfilePicture))
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(user.ProfilePicture);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    AvatarImage.ImageSource = bitmap;
                    AvatarText.Visibility = Visibility.Collapsed;
                }
                catch { AvatarText.Visibility = Visibility.Visible; }
            }
            else
            {
                AvatarText.Visibility = Visibility.Visible;
                AvatarImage.ImageSource = null;
            }

            // Set basic info
            FullNameText.Text = user.FullName ?? "N/A";
            UsernameText.Text = "@" + (user.Username ?? "N/A");
            RoleText.Text = user.Role ?? "Employee";
            StatusText.Text = user.IsActive ? "Active" : "Inactive";

            // Extended Details
            DetailEmailText.Text = user.Email ?? "N/A";
            DetailPhoneText.Text = user.Phone ?? "N/A";
            DetailPlaceText.Text = user.Place ?? "N/A";
            DetailDeptText.Text = user.Department ?? "N/A";
            DetailDesigText.Text = user.Designation ?? "N/A";
            DetailEducationText.Text = user.Education ?? "N/A";
            DetailEmergencyText.Text = user.EmergencyNumber ?? "N/A";
            DetailJoinDateText.Text = user.JoinDate?.ToString("dd-MMM-yyyy") ?? "N/A";
            DetailRelieveDateText.Text = user.RelievingDate?.ToString("dd-MMM-yyyy") ?? "N/A";

            // Update status badge color if inactive
            if (!user.IsActive)
            {
                StatusBadge.Style = (Style)FindResource("BadgeDanger");
                StatusText.Text = "Inactive";
                StatusText.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void LoadAttendance(NpgsqlConnection conn)
        {
            try
            {
                // Get all attendance records for the user
                _allAttendance = conn.Query<Attendance>(
                    @"SELECT * FROM Attendance 
                      WHERE UserId = @UserId 
                      ORDER BY Date DESC",
                    new { UserId = _userId }
                ).ToList();

                ApplyAttendanceFilters();

                // Calculate month stats for Overview
                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                int presentDays = _allAttendance.Count(a => a.Status == "Present" && a.Date >= startOfMonth && a.Date <= endOfMonth);
                int absentDays = _allAttendance.Count(a => a.Status == "Absent" && a.Date >= startOfMonth && a.Date <= endOfMonth);

                PresentDaysText.Text = presentDays.ToString();
                AbsentDaysText.Text = absentDays.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading attendance: {ex.Message}");
            }
        }

        private void GlobalFilter_Changed(object sender, EventArgs e)
        {
            if (_isDataLoading) return;
            
            _isDataLoading = true;
            try
            {
                // Sync global filters to specific filters
                AttendanceFilterFrom.SelectedDate = GlobalFilterFrom.SelectedDate;
                AttendanceFilterTo.SelectedDate = GlobalFilterTo.SelectedDate;
                
                WorkFilterFrom.SelectedDate = GlobalFilterFrom.SelectedDate;
                WorkFilterTo.SelectedDate = GlobalFilterTo.SelectedDate;
                WorkFilterSearch.Text = GlobalSearchBox.Text;
                
                QualityFilterFrom.SelectedDate = GlobalFilterFrom.SelectedDate;
                QualityFilterTo.SelectedDate = GlobalFilterTo.SelectedDate;
                
                ProjectSearchBox.Text = GlobalSearchBox.Text;
            }
            finally
            {
                _isDataLoading = false;
            }
            
            ApplyAttendanceFilters();
            ApplyWorkReportFilters();
            ApplyQualityFilters();
            ApplyProjectFilters();
        }

        private void ResetGlobalFilters_Click(object sender, RoutedEventArgs e)
        {
            _isDataLoading = true;
            try
            {
                GlobalFilterFrom.SelectedDate = null;
                GlobalFilterTo.SelectedDate = null;
                GlobalSearchBox.Text = "";
                
                // Sync reset
                AttendanceFilterFrom.SelectedDate = null;
                AttendanceFilterTo.SelectedDate = null;
                
                WorkFilterProject.SelectedIndex = 0;
                WorkFilterFrom.SelectedDate = null;
                WorkFilterTo.SelectedDate = null;
                WorkFilterSearch.Text = "";
                
                QualityFilterFrom.SelectedDate = null;
                QualityFilterTo.SelectedDate = null;
                
                ProjectSearchBox.Text = "";
            }
            finally
            {
                _isDataLoading = false;
            }
            
            ApplyAttendanceFilters();
            ApplyWorkReportFilters();
            ApplyQualityFilters();
            ApplyProjectFilters();
        }

        private void AttendanceFilter_Changed(object sender, EventArgs e)
        {
            if (_isDataLoading) return;
            ApplyAttendanceFilters();
        }

        private void ResetAttendanceFilters_Click(object sender, RoutedEventArgs e)
        {
            _isDataLoading = true;
            try
            {
                AttendanceFilterFrom.SelectedDate = null;
                AttendanceFilterTo.SelectedDate = null;
            }
            finally
            {
                _isDataLoading = false;
            }
            ApplyAttendanceFilters();
        }

        private void ApplyAttendanceFilters()
        {
            if (AttendanceGrid == null) return;

            var filtered = _allAttendance.AsEnumerable();

            if (AttendanceFilterFrom.SelectedDate.HasValue)
            {
                filtered = filtered.Where(a => a.Date.Date >= AttendanceFilterFrom.SelectedDate.Value.Date);
            }
            if (AttendanceFilterTo.SelectedDate.HasValue)
            {
                filtered = filtered.Where(a => a.Date.Date <= AttendanceFilterTo.SelectedDate.Value.Date);
            }

            AttendanceGrid.ItemsSource = filtered.ToList();
        }

        private void LoadLeaveData(NpgsqlConnection conn)
        {
            try
            {
                var leaves = conn.Query<LeaveRequestDisplay>(
                    @"SELECT * FROM Leaves 
                      WHERE UserId = @UserId 
                      ORDER BY StartDate DESC",
                    new { UserId = _userId }
                ).ToList();

                // Count by status
                int approved = leaves.Count(l => l.Status == "Approved");
                int pending = leaves.Count(l => l.Status == "Pending");

                ApprovedLeaveText.Text = approved.ToString();
                PendingLeaveText.Text = pending.ToString();

                LeaveGrid.ItemsSource = leaves;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading leave data: {ex.Message}");
            }
        }

        private void LoadWorkReports(NpgsqlConnection conn)
        {
            try
            {
                // Get all work reports for the user
                _allWorkReports = conn.Query<WorkReportDisplay>(
                    @"SELECT wr.Id, wr.SubmissionDate as Date, p.Name as ProjectName, wr.ObjectId, wr.BillingAmount
                      FROM WorkReports wr
                      LEFT JOIN Projects p ON wr.ProjectId = p.Id
                      WHERE wr.UserId = @UserId 
                      ORDER BY wr.SubmissionDate DESC",
                    new { UserId = _userId }
                ).ToList();

                ApplyWorkReportFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading work reports: {ex.Message}");
            }
        }

        private void WorkFilter_Changed(object sender, EventArgs e)
        {
            if (_isDataLoading) return;
            ApplyWorkReportFilters();
        }

        private void ResetWorkFilters_Click(object sender, RoutedEventArgs e)
        {
            _isDataLoading = true;
            try
            {
                WorkFilterProject.SelectedIndex = 0;
                WorkFilterFrom.SelectedDate = null;
                WorkFilterTo.SelectedDate = null;
                WorkFilterSearch.Text = "";
            }
            finally
            {
                _isDataLoading = false;
            }
            ApplyWorkReportFilters();
        }

        private void ApplyWorkReportFilters()
        {
            if (WorkReportsGrid == null) return;

            var filtered = _allWorkReports.AsEnumerable();

            // Project Filter
            if (WorkFilterProject.SelectedItem is ComboBoxItem pi && pi.Content.ToString() != "All Projects")
            {
                filtered = filtered.Where(r => r.ProjectName == pi.Content.ToString());
            }

            // Date Filters
            if (WorkFilterFrom.SelectedDate.HasValue)
            {
                filtered = filtered.Where(r => r.Date.Date >= WorkFilterFrom.SelectedDate.Value.Date);
            }
            if (WorkFilterTo.SelectedDate.HasValue)
            {
                filtered = filtered.Where(r => r.Date.Date <= WorkFilterTo.SelectedDate.Value.Date);
            }

            // Global Search (Object ID)
            string search = WorkFilterSearch.Text?.ToLower();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(r => 
                    (r.ObjectId != null && r.ObjectId.ToLower().Contains(search)) ||
                    (r.ProjectName != null && r.ProjectName.ToLower().Contains(search)));
            }

            var result = filtered.ToList();
            WorkReportsGrid.ItemsSource = result;

            // Update Total Amount for the filtered view
            decimal total = result.Sum(r => r.BillingAmount);
            WorkReportsTotalAmount.Text = " - Filtered Total: ₹" + total.ToString("N2");
        }

        private void LoadQualityReports(NpgsqlConnection conn)
        {
            try
            {
                var reports = conn.Query<QualityReportDisplay>(
                    @"SELECT Id, ReportDate as Date, QualityScore, ErrorRate, ReworkCount, Remarks
                      FROM QualityReports 
                      WHERE UserId = @UserId 
                      ORDER BY ReportDate DESC",
                    new { UserId = _userId }
                ).ToList();

                // Calculate averages
                if (reports.Any())
                {
                    double avgQuality = reports.Average(r => r.QualityScore);
                    double avgError = reports.Average(r => r.ErrorRate);

                    AvgQualityText.Text = avgQuality.ToString("N1") + "%";
                    AvgErrorRateText.Text = avgError.ToString("N2") + "%";

                    // Color code error rate
                    if (avgError > 5)
                        AvgErrorRateText.Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
                    else if (avgError > 2)
                        AvgErrorRateText.Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D97706"));
                    else
                        AvgErrorRateText.Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#059669"));
                }
                else
                {
                    AvgQualityText.Text = "N/A";
                    AvgErrorRateText.Text = "N/A";
                }

                TotalQualityReportsText.Text = reports.Count.ToString();
                QualitySummaryText.Text = $"Avg: {AvgQualityText.Text} | Error Rate: {AvgErrorRateText.Text}";
                
                _allQualityReports = reports;
                ApplyQualityFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading quality reports: {ex.Message}");
            }
        }

        private void QualityFilter_Changed(object sender, EventArgs e)
        {
            if (_isDataLoading) return;
            ApplyQualityFilters();
        }

        private void ResetQualityFilters_Click(object sender, RoutedEventArgs e)
        {
            _isDataLoading = true;
            try
            {
                QualityFilterFrom.SelectedDate = null;
                QualityFilterTo.SelectedDate = null;
            }
            finally
            {
                _isDataLoading = false;
            }
            ApplyQualityFilters();
        }

        private void ApplyQualityFilters()
        {
            if (QualityGrid == null) return;
            
            var filtered = _allQualityReports.AsEnumerable();
            
            if (QualityFilterFrom.SelectedDate.HasValue)
                filtered = filtered.Where(r => r.Date.Date >= QualityFilterFrom.SelectedDate.Value.Date);
            
            if (QualityFilterTo.SelectedDate.HasValue)
                filtered = filtered.Where(r => r.Date.Date <= QualityFilterTo.SelectedDate.Value.Date);
                
            QualityGrid.ItemsSource = filtered.ToList();
        }

        private void LoadProjects(NpgsqlConnection conn)
        {
            try
            {
                var projects = conn.Query<ProjectDisplay>(
                    @"SELECT p.Name as ProjectName, 
                             COUNT(wr.Id) as ReportsCount, 
                             COALESCE(SUM(wr.BillingAmount), 0) as TotalAmount,
                             MAX(wr.SubmissionDate) as LastReportDate
                      FROM Projects p
                      LEFT JOIN WorkReports wr ON p.Id = wr.ProjectId AND wr.UserId = @UserId
                      GROUP BY p.Id, p.Name
                      HAVING COUNT(wr.Id) > 0
                      ORDER BY ReportsCount DESC",
                    new { UserId = _userId }
                ).ToList();

                TotalProjectsText.Text = projects.Count.ToString();

                // Show project names in summary
                if (projects.Any())
                {
                    var projectNames = projects.Select(p => p.ProjectName).Take(3).ToList();
                    string names = string.Join(", ", projectNames);
                    if (projects.Count > 3)
                        names += $" (+{projects.Count - 3} more)";
                    ProjectsListText.Text = names;
                }
                else
                {
                    ProjectsListText.Text = "No projects yet";
                }

                ProjectsGrid.ItemsSource = projects;
                _allProjects = projects;
                ApplyProjectFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading projects: {ex.Message}");
            }
        }

        private void ProjectFilter_Changed(object sender, EventArgs e)
        {
            if (_isDataLoading) return;
            ApplyProjectFilters();
        }

        private void ApplyProjectFilters()
        {
            if (ProjectsGrid == null) return;
            
            var filtered = _allProjects.AsEnumerable();
            
            string search = ProjectSearchBox.Text?.ToLower();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(p => p.ProjectName.ToLower().Contains(search));
            }
            
            ProjectsGrid.ItemsSource = filtered.ToList();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
