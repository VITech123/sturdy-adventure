using System;
using System.Collections.Generic;
using Npgsql;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using LiveCharts;
using LiveCharts.Wpf;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class DashboardPage : Page
    {
        public string[] ChartLabels { get; set; }
        public Func<double, string> ChartFormatter { get; set; }
        
        private DispatcherTimer _refreshTimer;

        public DashboardPage()
        {
            InitializeComponent();
            DataContext = this;
            SubtitleText.Text = $"Good {GetGreeting()}, {SessionManager.CurrentUser?.FullName ?? "User"}";
            ChartFormatter = value => "₹" + value.ToString("N0");
            
            ApplyPersonalization();
            
            if (!SessionManager.IsAdmin)
            {
                AdminPathBtn.Visibility = System.Windows.Visibility.Collapsed;
                AdminDailyPerformanceCard.Visibility = Visibility.Collapsed;
                ResumePipelineCard.Visibility = Visibility.Collapsed;
            }
            else
            {
                AdminPathBtn.Visibility = System.Windows.Visibility.Visible;
                AdminDailyPerformanceCard.Visibility = Visibility.Visible;
                ResumePipelineCard.Visibility = Visibility.Visible;
            }

            LoadData();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _refreshTimer.Tick += (s, e) => LoadData();
            this.Loaded += (s, e) => _refreshTimer.Start();
            this.Unloaded += (s, e) => _refreshTimer.Stop();
        }

        private string GetGreeting()
        {
            int hour = DateTime.Now.Hour;
            if (hour < 12) return "morning";
            if (hour < 17) return "afternoon";
            return "evening";
        }

        private void ApplyPersonalization()
        {
            var user = SessionManager.CurrentUser;
            if (user == null) return;

            // Apply Wallpaper
            if (!string.IsNullOrEmpty(user.WallpaperPath) && File.Exists(user.WallpaperPath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(user.WallpaperPath, UriKind.Absolute);
                    bmp.EndInit();
                    DashboardWallpaper.Source = bmp;
                }
                catch { }
            }

            // Apply Theme
            if (!string.IsNullOrEmpty(user.SelectedTheme))
            {
                string themeFile = user.SelectedTheme switch
                {
                    "Emerald Green" => "PastelGreen.xaml",
                    "Deep Purple" => "PastelPurple.xaml",
                    "Sunset Orange" => "PastelOrange.xaml",
                    _ => "PastelBlue.xaml"
                };

                try
                {
                    // Update application-wide resources for the current session
                    var uri = new Uri($"/EnterpriseWorkReport;component/Themes/{themeFile}", UriKind.RelativeOrAbsolute);
                    var dict = Application.LoadComponent(uri) as ResourceDictionary;
                    if (dict != null)
                    {
                        var merged = Application.Current.Resources.MergedDictionaries;
                        
                        // Find and replace the existing theme colors dictionary
                        ResourceDictionary existingTheme = null;
                        foreach (var d in merged)
                        {
                            if (d.Source != null && d.Source.OriginalString.Contains("Themes/") && 
                                !d.Source.OriginalString.Contains("Styles.xaml"))
                            {
                                existingTheme = d;
                                break;
                            }
                        }

                        if (existingTheme != null)
                        {
                            int index = merged.IndexOf(existingTheme);
                            merged[index] = dict;
                        }
                        else
                        {
                            merged.Add(dict);
                        }
                    }
                }
                catch { }
            }
        }

        private void LoadResumeStats(NpgsqlConnection conn)
        {
            try
            {
                var stats = conn.Query("SELECT Status, COUNT(*) as Count FROM Resumes GROUP BY Status")
                                .ToDictionary(x => (string)x.status, x => (long)x.count);

                ResumesPendingText.Text = stats.ContainsKey("Pending") ? stats["Pending"].ToString() : "0";
                ResumesOngoingText.Text = stats.ContainsKey("Ongoing") ? stats["Ongoing"].ToString() : "0";
                ResumesRejectedText.Text = stats.ContainsKey("Rejected") ? stats["Rejected"].ToString() : "0";
                ResumesRelievedText.Text = stats.ContainsKey("Relieved") ? stats["Relieved"].ToString() : "0";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading resume stats: {ex.Message}");
            }
        }

        private void LoadData()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var userId = SessionManager.CurrentUser?.Id ?? 0;
                bool isAdmin = SessionManager.IsAdmin;
                DateTime today = DateTime.Today;

                if (isAdmin)
                {
                    LoadResumeStats(conn);
                }

                // --- 1. KPI Cards ---
                string where = isAdmin ? "WHERE CAST(wr.SubmissionDate AS DATE) = @D" : "WHERE wr.UserId = @U AND CAST(wr.SubmissionDate AS DATE) = @D";
                
                var todaysReports = conn.Query<WorkReport>($@"
                    SELECT wr.* 
                    FROM WorkReports wr 
                    {where}", new { U = userId, D = today }).ToList();

                int totalObjects = todaysReports.Count;
                decimal totalBilling = todaysReports.Sum(r => r.BillingAmount);
                int totalPages = 0;
                int totalChars = 0;

                if (totalObjects > 0)
                {
                    var reportIds = todaysReports.Select(r => r.Id).ToArray();
                    var itemsSql = @"
                        SELECT wri.*, pf.FieldLabel
                        FROM WorkReportItems wri
                        JOIN ProjectFields pf ON pf.Id = wri.FieldId
                        WHERE wri.WorkReportId = ANY(@Ids)";
                    var allItems = conn.Query<WorkReportItem>(itemsSql, new { Ids = reportIds }).ToList();

                    foreach (var report in todaysReports)
                    {
                        report.Items = allItems.Where(i => i.WorkReportId == report.Id).ToList();
                        totalPages += report.PagesCount;
                        totalChars += report.CharacterCount;
                    }
                }

                TotalObjectsText.Text = totalObjects.ToString();
                if (TotalPagesText != null) TotalPagesText.Text = totalPages.ToString();
                if (TotalCharsText != null) TotalCharsText.Text = totalChars.ToString();
                TotalBillingText.Text = $"₹{totalBilling:F2}";

                // -- Admin Daily Performance --
                if (isAdmin && AdminDailyPerformanceCard != null && AdminDailyPerformanceGrid != null)
                {
                    AdminDailyPerformanceCard.Visibility = System.Windows.Visibility.Visible;
                    
                    var userReports = conn.Query($@"
                        SELECT u.FullName as EmployeeName, wr.Id as ReportId, wr.BillingAmount
                        FROM WorkReports wr
                        JOIN Users u ON u.Id = wr.UserId
                        WHERE CAST(wr.SubmissionDate AS DATE) = @D", new { D = today }).ToList();

                    var rIds = userReports.Select(r => (int)r.ReportId).Distinct().ToArray(); // Use array for Npgsql ANY()
                    var allItems = rIds.Any() ? conn.Query(@"
                        SELECT wri.WorkReportId, wri.Value, pf.FieldLabel
                        FROM WorkReportItems wri
                        JOIN ProjectFields pf ON pf.Id = wri.FieldId
                        WHERE wri.WorkReportId = ANY(@Ids)", new { Ids = rIds }).ToList() : new List<dynamic>();

                    var perfData = new List<dynamic>();
                    var grouped = userReports.GroupBy(r => (string)r.EmployeeName);
                    foreach (var g in grouped)
                    {
                        int uObjs = g.Count();
                        decimal uAmt = g.Sum(r => (decimal)r.BillingAmount);
                        int uPages = 0;
                        int uChars = 0;

                        foreach (var r in g)
                        {
                            var rItems = allItems.Where(i => i.WorkReportId == r.ReportId);
                            foreach (var item in rItems)
                            {
                                string label = item.FieldLabel ?? "";
                                string val = item.Value;
                                if (label.IndexOf("page", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    if (int.TryParse(val, out int p)) uPages += p;
                                }
                                else if (label.IndexOf("char", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    if (int.TryParse(val, out int c)) uChars += c;
                                }
                            }
                        }

                        perfData.Add(new
                        {
                            EmployeeName = g.Key,
                            ObjectsCount = uObjs,
                            PagesCount = uPages,
                            CharsCount = uChars,
                            Amount = uAmt
                        });
                    }
                    
                    AdminDailyPerformanceGrid.ItemsSource = perfData;
                }
                
                // --- 2. Attendance Status & Buttons ---
                var todayAtt = conn.QueryFirstOrDefault<Attendance>("SELECT * FROM Attendance WHERE UserId=@U AND Date=@D", new { U = userId, D = today });
                if (todayAtt == null)
                {
                    DashboardAttendanceStatus.Text = "Status: Not Clocked In";
                    ClockInBtn.IsEnabled = true;
                    ClockOutBtn.IsEnabled = false;
                }
                else if (string.IsNullOrEmpty(todayAtt.ClockOutTime))
                {
                    DashboardAttendanceStatus.Text = $"Status: Clocked in at {todayAtt.ClockInTime}";
                    ClockInBtn.IsEnabled = false;
                    ClockOutBtn.IsEnabled = true;
                }
                else
                {
                    DashboardAttendanceStatus.Text = $"Status: Completed ({todayAtt.ClockInTime} - {todayAtt.ClockOutTime})";
                    ClockInBtn.IsEnabled = false;
                    ClockOutBtn.IsEnabled = false;
                }

                // -- 3. Recent Reports --
                var recentReports = conn.Query<WorkReport>(@$"
                    SELECT wr.*, u.FullName AS EmployeeName, p.Name AS ProjectName 
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    JOIN Projects p ON p.Id = wr.ProjectId
                    {(isAdmin ? "" : $"WHERE wr.UserId = {userId}")}
                    ORDER BY wr.SubmissionDate DESC LIMIT 20");
                RecentReportsGrid.ItemsSource = recentReports;

                // -- 4. Mini Leaderboards --
                var topQuality = conn.Query(@"
                    SELECT u.FullName AS ""FullName"", AVG(qr.QualityScore) AS ""Score"",
                           ROW_NUMBER() OVER (ORDER BY AVG(qr.QualityScore) DESC) AS ""Rank""
                    FROM QualityReports qr
                    JOIN Users u ON u.Id = qr.UserId
                    WHERE to_char(qr.ReportDate, 'YYYY-MM') = to_char(CURRENT_DATE, 'YYYY-MM')
                    GROUP BY u.Id ORDER BY ""Score"" DESC LIMIT 3");
                TopQualityList.ItemsSource = topQuality;

                var topWorkCount = conn.Query(@"
                    SELECT u.FullName AS ""FullName"", COUNT(wr.Id) AS ""ReportCount"",
                           ROW_NUMBER() OVER (ORDER BY COUNT(wr.Id) DESC) AS ""Rank""
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    WHERE to_char(wr.SubmissionDate, 'YYYY-MM') = to_char(CURRENT_DATE, 'YYYY-MM')
                    GROUP BY u.Id ORDER BY ""ReportCount"" DESC LIMIT 3");
                TopWorkCountList.ItemsSource = topWorkCount;

                // -- 5. Recent Messages --
                var msgService = new MessageService();
                var recentMsg = msgService.GetInbox(userId).Take(3).ToList();
                RecentMessagesList.ItemsSource = recentMsg;

                // -- 6. Charts --
                LoadWeeklyTrend(conn);
                LoadProjectDistribution(conn);
                LoadAttendanceTrend(conn);
                LoadQualityTrend(conn);
            }
        }

        private void LoadAttendanceTrend(NpgsqlConnection conn)
        {
            var last7Days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(-i)).Reverse().ToList();
            var userId = SessionManager.CurrentUser?.Id ?? 0;
            bool isAdmin = SessionManager.IsAdmin;

            var attData = conn.Query<(string Day, long Count)>(@$"
                SELECT to_char(Date, 'YYYY-MM-DD') as Day, COUNT(*) as Count
                FROM Attendance
                WHERE Date >= @Start
                {(isAdmin ? "" : "AND UserId = @U")}
                GROUP BY Day", new { Start = last7Days.First(), U = userId });

            var values = new ChartValues<int>();
            foreach (var day in last7Days)
            {
                var dayStr = day.ToString("yyyy-MM-dd");
                var match = attData.FirstOrDefault(d => d.Day == dayStr);
                values.Add((int)match.Count);
            }

            AttendanceChart.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Attendance",
                    Values = values,
                    Fill = new SolidColorBrush(Color.FromRgb(59, 130, 246))
                }
            };
        }

        private void LoadQualityTrend(NpgsqlConnection conn)
        {
            var last7Days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(-i)).Reverse().ToList();
            var userId = SessionManager.CurrentUser?.Id ?? 0;
            bool isAdmin = SessionManager.IsAdmin;

            var qualData = conn.Query<(string Day, double Avg)>(@$"
                SELECT to_char(ReportDate, 'YYYY-MM-DD') as Day, AVG(QualityScore) as Avg
                FROM QualityReports
                WHERE ReportDate >= @Start
                {(isAdmin ? "" : "AND UserId = @U")}
                GROUP BY Day", new { Start = last7Days.First(), U = userId });

            var values = new ChartValues<double>();
            foreach (var day in last7Days)
            {
                var dayStr = day.ToString("yyyy-MM-dd");
                var match = qualData.FirstOrDefault(d => d.Day == dayStr);
                values.Add(match.Avg);
            }

            QualityTrendChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Avg Quality %",
                    Values = values,
                    Stroke = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    Fill = Brushes.Transparent
                }
            };
        }

        private void ClockInBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var userId = SessionManager.CurrentUser?.Id ?? 0;
            DateTime today = DateTime.Today;
            string time = DateTime.Now.ToString("HH:mm");

            using (var conn = DatabaseService.GetConnection())
            {
                long count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Attendance WHERE UserId=@U AND Date=@D", new { U = userId, D = today });
                if (count == 0)
                {
                    conn.Execute("INSERT INTO Attendance (UserId, Date, Status, ClockInTime) VALUES (@U, @D, 'Present', @T)",
                        new { U = userId, D = today, T = time });
                    LoadData();
                }
            }
        }

        private void ClockOutBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var userId = SessionManager.CurrentUser?.Id ?? 0;
            DateTime today = DateTime.Today;
            string time = DateTime.Now.ToString("HH:mm");

            using (var conn = DatabaseService.GetConnection())
            {
                var att = conn.QueryFirstOrDefault<Attendance>("SELECT * FROM Attendance WHERE UserId=@U AND Date=@D", new { U = userId, D = today });
                if (att != null && string.IsNullOrEmpty(att.ClockOutTime))
                {
                    double hoursWorked = 0;
                    if (DateTime.TryParse(att.ClockInTime, out DateTime inTime) && DateTime.TryParse(time, out DateTime outTime))
                    {
                        if (outTime < inTime) outTime = outTime.AddDays(1); // Crossed midnight
                        hoursWorked = (outTime - inTime).TotalHours;
                    }

                    conn.Execute("UPDATE Attendance SET ClockOutTime=@T, HoursWorked=@H WHERE Id=@Id",
                        new { T = time, H = hoursWorked, Id = att.Id });
                    LoadData();
                }
            }
        }

        private void LoadWeeklyTrend(NpgsqlConnection conn)
        {
            var last7Days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(-i)).Reverse().ToList();
            ChartLabels = last7Days.Select(d => d.ToString("dd MMM")).ToArray();

            var userId = SessionManager.CurrentUser?.Id ?? 0;
            bool isAdmin = SessionManager.IsAdmin;

            var trendData = conn.Query<(string Day, double Total)>(@$"
                SELECT to_char(SubmissionDate, 'YYYY-MM-DD') as Day, SUM(BillingAmount) as Total
                FROM WorkReports
                WHERE CAST(SubmissionDate AS DATE) >= @Start
                {(isAdmin ? "" : "AND UserId = @U")}
                GROUP BY Day", new { Start = last7Days.First(), U = userId });
            
            // ... rest of the method unchanged but I'll replace the loop too for safety
            var values = new ChartValues<double>();
            foreach (var day in last7Days)
            {
                var dayStr = day.ToString("yyyy-MM-dd");
                var match = trendData.FirstOrDefault(d => d.Day == dayStr);
                values.Add(match.Total);
            }


            BillingChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Daily Billing",
                    Values = values,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 10,
                    StrokeThickness = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 33, 150, 243)) // Light blue fill
                }
            };
        }

        private void LoadProjectDistribution(NpgsqlConnection conn)
        {
            var userId = SessionManager.CurrentUser?.Id ?? 0;
            bool isAdmin = SessionManager.IsAdmin;

            var projectData = conn.Query<(string ProjectName, double Total)>(@$"
                SELECT p.Name as ProjectName, SUM(wr.BillingAmount) as Total
                FROM WorkReports wr
                JOIN Projects p ON p.Id = wr.ProjectId
                {(isAdmin ? "" : $"WHERE wr.UserId = {userId}")}
                GROUP BY p.Id ORDER BY Total DESC");

            var series = new SeriesCollection();
            foreach (var p in projectData)
            {
                series.Add(new PieSeries
                {
                    Title = p.ProjectName,
                    Values = new ChartValues<double> { p.Total },
                    DataLabels = true
                });
            }
            ProjectPieChart.Series = series;
        }

        private void AdminPathBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as MainShellWindow;
            mainWindow?.NavigateTo("Settings");
        }
    }
}
