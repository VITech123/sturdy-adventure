using System;
using System.Collections.Generic;
using Npgsql;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class AnalyticsPage : Page
    {
        private MasterDataService _masterDataService = new MasterDataService();
        private int? _selectedProjectId;
        private DateTime? _fromDate;
        private DateTime? _toDate;

        public string[] DateLabels { get; set; }
        public Func<double, string> NumberFormatter { get; set; }

        public AnalyticsPage()
        {
            InitializeComponent();
            DataContext = this;
            NumberFormatter = val => val.ToString("N0");
            LoadFilters();
            LoadData();
            BackgroundSyncService.Instance.MasterDataUpdated += OnMasterDataUpdated;
        }

        private void OnMasterDataUpdated(int projectId)
        {
            if (!_selectedProjectId.HasValue || _selectedProjectId.Value != projectId) return;
            Dispatcher.Invoke(() =>
            {
                SyncBtn.Content = "⚡ Sync with Master";
                LoadData();
                NotificationService.Show("Master File Updated", "Master file changes have been synced to the dashboard.", NotificationType.Success);
            });
        }

        private void LoadFilters()
        {
            using var conn = DatabaseService.GetConnection();
            var projects = conn.Query<Project>("SELECT * FROM Projects WHERE IsActive = 1 ORDER BY Name").ToList();
            projects.Insert(0, new Project { Id = 0, Name = "All Projects" });

            FilterProject.ItemsSource = projects;
            FilterProject.DisplayMemberPath = "Name";
            FilterProject.SelectedValuePath = "Id";
            FilterProject.SelectedIndex = 0;
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (FilterProject == null || FilterFromDate == null || FilterToDate == null) return;
            
            if (FilterProject.SelectedValue != null && FilterProject.SelectedValue is int projectId)
            {
                _selectedProjectId = projectId > 0 ? projectId : null;
                UpdateSyncButtonVisibility();
            }
            _fromDate = FilterFromDate.SelectedDate;
            _toDate = FilterToDate.SelectedDate;
            if (this.IsLoaded) LoadData();
        }

        private void UpdateSyncButtonVisibility()
        {
            if (SyncBtn == null) return;
            if (_selectedProjectId.HasValue && SessionManager.IsAdmin)
            {
                using var conn = DatabaseService.GetConnection();
                var project = conn.QueryFirstOrDefault<Project>("SELECT * FROM Projects WHERE Id = @Id", new { Id = _selectedProjectId.Value });
                if (project != null && !string.IsNullOrEmpty(project.MasterFilePath))
                {
                    SyncBtn.Visibility = Visibility.Visible;
                    SyncBtn.ToolTip = $"Source: {project.MasterFilePath}";
                }
                else
                {
                    SyncBtn.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                SyncBtn.Visibility = Visibility.Collapsed;
            }
        }

        private void Sync_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedProjectId.HasValue) return;

            try
            {
                using var conn = DatabaseService.GetConnection();
                var project = conn.QueryFirstOrDefault<Project>("SELECT * FROM Projects WHERE Id = @Id", new { Id = _selectedProjectId.Value });
                if (project == null || string.IsNullOrEmpty(project.MasterFilePath)) return;
                project.MasterFilePassword = SecretService.DecryptSecret(project.MasterFilePassword);

                if (!File.Exists(project.MasterFilePath))
                {
                    MessageBox.Show($"Master file not found at: {project.MasterFilePath}\nPlease update the path in Project Settings.", "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SyncBtn.IsEnabled = false;
                SyncBtn.Content = "⌛ Syncing...";

                var nameMatcher = new FuzzyNameMatcher();
                var settings = conn.QueryFirstOrDefault<CompanySettings>("SELECT MasterNamesPath FROM CompanySettings WHERE Id = 1");
                if (settings != null && !string.IsNullOrEmpty(settings.MasterNamesPath))
                    nameMatcher.LoadMasterNames(settings.MasterNamesPath);

                int imported = 0;
                try
                {
                    imported = _masterDataService.SyncMasterFile(project.Id, project.MasterFilePath, true, project.MasterFilePassword, nameMatcher);
                }
                catch (Exception ex) when (ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("encrypt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var pwdDialog = new PasswordDialog();
                    if (pwdDialog.ShowDialog() == true)
                    {
                        string pwd = pwdDialog.Password;
                        // Update stored password (encrypted)
                        string encrypted = SecretService.EncryptSecret(pwd);
                        conn.Execute("UPDATE Projects SET MasterFilePassword = @P WHERE Id = @Id", new { P = encrypted, Id = project.Id });
                        // Retry with new password
                        imported = _masterDataService.SyncMasterFile(project.Id, project.MasterFilePath, true, pwd, nameMatcher);
                    }
                    else
                    {
                        MessageBox.Show("Password required to sync master file.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                MessageBox.Show($"✅ Sync Complete! Imported {imported} records.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sync failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SyncBtn.IsEnabled = true;
                SyncBtn.Content = "⚡ Sync with Master";
            }
        }

        private void Period_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (FilterPeriod == null || FilterFromDate == null || FilterToDate == null) return;
            
            if (FilterPeriod.SelectedItem is ComboBoxItem item && item.Tag is string period)
            {
                var today = DateTime.Today;
                switch (period)
                {
                    case "today":
                        _fromDate = today;
                        _toDate = today;
                        break;
                    case "week":
                        _fromDate = today.AddDays(-(int)today.DayOfWeek);
                        _toDate = today;
                        break;
                    case "month":
                        _fromDate = new DateTime(today.Year, today.Month, 1);
                        _toDate = today;
                        break;
                    case "all":
                        _fromDate = null;
                        _toDate = null;
                        break;
                }
                FilterFromDate.SelectedDate = _fromDate;
                FilterToDate.SelectedDate = _toDate;
                if (this.IsLoaded) LoadData();
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            using var conn = DatabaseService.GetConnection();
            bool isAdmin = SessionManager.IsAdmin;

            // Load Master Data Summary
            var summary = _masterDataService.GetSummary(_selectedProjectId, _fromDate, _toDate);
            TotalManifestsText.Text = summary.TotalManifests.ToString("N0");
            TotalObjectsText.Text = summary.TotalObjects.ToString("N0");
            TotalPagesText.Text = summary.TotalPages.ToString("N0");
            TotalArticlesText.Text = summary.TotalArticles.ToString("N0");
            TotalCharactersText.Text = summary.TotalCharacters.ToString("N0");

            ShippedCountText.Text = summary.ShippedCount.ToString("N0");
            UnassignedCountText.Text = summary.UnassignedCount.ToString("N0");
            HoldCountText.Text = summary.HoldCount.ToString("N0");
            ErrorCountText.Text = summary.ErrorCount.ToString("N0");

            // Today's Manifest Count
            int todayManifestCount = _masterDataService.GetTodayManifestCount(_selectedProjectId);
            TodayManifestsText.Text = todayManifestCount.ToString("N0");

            // Load Manifest Details for current date (based on EndDate from batch)
            var manifestDetails = _masterDataService.GetTodayManifestDetails(_selectedProjectId);
            ManifestDetailsGrid.ItemsSource = manifestDetails;

            // Load Status Breakdown Charts
            var statusBreakdown = _masterDataService.GetStatusBreakdown(_selectedProjectId, _fromDate, _toDate);
            LoadStatusCharts(statusBreakdown);

            // Load Errors
            var errors = _masterDataService.GetErrors();
            ErrorsGrid.ItemsSource = errors;
            ErrorCountHeader.Text = $"({errors.Count} errors)";

            // Load Master Data Grid
            var masterData = _masterDataService.GetAll(_selectedProjectId, null, _fromDate, _toDate);
            MasterDataGrid.ItemsSource = masterData;
            RecordCountText.Text = $"({masterData.Count} records)";

            // Load Work Reports Stats
            LoadWorkReportsStats(conn);

            // Load Quality Trend
            LoadQualityTrend(conn);

            UpdateGridHeaders();
        }

        private void UpdateGridHeaders()
        {
            if (MasterDataGrid == null) return;

            string objectIdHeader = "Object ID";
            string pagesHeader = "Pages";
            string charactersHeader = "Characters";

            if (_selectedProjectId.HasValue)
            {
                using var conn = DatabaseService.GetConnection();
                var project = conn.QueryFirstOrDefault<Project>("SELECT * FROM Projects WHERE Id = @Id", new { Id = _selectedProjectId.Value });
                if (project != null)
                {
                    if (project.Name.Contains("142"))
                    {
                        objectIdHeader = "object_Id";
                        charactersHeader = "charactercount";
                    }
                    else if (project.Name.Contains("143"))
                    {
                        objectIdHeader = "object_Id";
                        pagesHeader = "pages";
                        charactersHeader = "charactercount";
                    }
                }
            }

            foreach (var col in MasterDataGrid.Columns)
            {
                if (col.Header.ToString() == "Object ID" || col.Header.ToString() == "object_Id")
                    col.Header = objectIdHeader;
                else if (col.Header.ToString() == "Pages" || col.Header.ToString() == "pages")
                    col.Header = pagesHeader;
                else if (col.Header.ToString() == "Characters" || col.Header.ToString() == "charactercount")
                    col.Header = charactersHeader;
            }
        }

        private void LoadStatusCharts(List<MasterDataStatusSummary> statusData)
        {
            var pieSeries = new SeriesCollection();
            var barSeries = new SeriesCollection();
            var colors = new Dictionary<string, System.Windows.Media.Color>
            {
                { "Shipped", System.Windows.Media.Color.FromRgb(5, 150, 105) },
                { "Unassigned", System.Windows.Media.Color.FromRgb(217, 119, 6) },
                { "Hold", System.Windows.Media.Color.FromRgb(37, 99, 235) },
                { "Error", System.Windows.Media.Color.FromRgb(220, 38, 38) },
                { "Assigned", System.Windows.Media.Color.FromRgb(107, 114, 128) },
                { "Pending", System.Windows.Media.Color.FromRgb(139, 92, 246) } // Purple for Pending
            };

            foreach (var status in statusData)
            {
                var color = colors.ContainsKey(status.Status) ? colors[status.Status] : System.Windows.Media.Color.FromRgb(107, 114, 128);
                var displayStatus = status.Status ?? "Unassigned";

                pieSeries.Add(new PieSeries
                {
                    Title = displayStatus,
                    Values = new ChartValues<double> { status.Count },
                    Fill = new System.Windows.Media.SolidColorBrush(color),
                    DataLabels = true
                });

                barSeries.Add(new ColumnSeries
                {
                    Title = displayStatus,
                    Values = new ChartValues<double> { status.Count },
                    Fill = new System.Windows.Media.SolidColorBrush(color)
                });
            }

            StatusPieChart.Series = pieSeries;
            StatusBarChart.Series = barSeries;
        }

        private void LoadWorkReportsStats(NpgsqlConnection conn)
        {
            var today = DateTime.Today;
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            ReportsTodayText.Text = conn.ExecuteScalar<long>(@"
                SELECT COUNT(*) FROM WorkReports WHERE CAST(SubmissionDate AS DATE) = @Today", new { Today = today }).ToString();

            ReportsWeekText.Text = conn.ExecuteScalar<long>(@"
                SELECT COUNT(*) FROM WorkReports WHERE CAST(SubmissionDate AS DATE) >= @Start", new { Start = weekStart }).ToString();

            ReportsMonthText.Text = conn.ExecuteScalar<long>(@"
                SELECT COUNT(*) FROM WorkReports WHERE CAST(SubmissionDate AS DATE) >= @Start", new { Start = monthStart }).ToString();
        }

        private void LoadQualityTrend(NpgsqlConnection conn)
        {
            var last14Days = Enumerable.Range(0, 14).Select(i => DateTime.Today.AddDays(-i)).Reverse().ToList();
            DateLabels = last14Days.Select(d => d.ToString("dd MMM")).ToArray();

            var qualityData = conn.Query<(DateTime Day, double AvgQuality)>(@"
                SELECT CAST(ReportDate AS DATE) as Day, AVG(QualityScore) as AvgQuality
                FROM QualityReports
                WHERE ReportDate >= @Start
                GROUP BY Day",
                new { Start = last14Days.First() });

            var values = new ChartValues<double>();
            foreach (var day in last14Days)
            {
                var match = qualityData.FirstOrDefault(d => d.Day.Date == day.Date);
                values.Add(match.AvgQuality);
            }

            QualityTrendChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Quality Score",
                    Values = values,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 6,
                    StrokeThickness = 2,
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 34, 147, 218))
                }
            };
        }
    }
}
