using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;
using Microsoft.Win32;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class WorkReportsPage : Page
    {
        private List<WorkReport> _allReports = new();
        private string _globalSearchQuery = "";
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalCount = 0;

        public WorkReportsPage()
        {
            try
            {
                InitializeComponent();
                LoadProjectFilter();
                
                if (FilterProject != null && FilterProject.Items.Count > 0)
                    FilterProject.SelectedIndex = 0;
                
                if (PageSizeCombo != null)
                    PageSizeCombo.SelectedIndex = 1; 

                if (!SessionManager.IsAdmin)
                {
                    if (EmployeeCol != null) EmployeeCol.Visibility = Visibility.Collapsed;
                    if (FilterEmployeeName != null) FilterEmployeeName.Visibility = Visibility.Collapsed;
                }
                
                LoadReports();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Work Reports Page failed to load: {ex.Message}\n\nStack Trace: {ex.StackTrace}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"WorkReportsPage Init Error: {ex}");
            }
        }

        private void LoadProjectFilter()
        {
            if (FilterProject == null) return;
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var projects = conn.Query<Project>("SELECT * FROM Projects WHERE IsActive=1 ORDER BY Name").AsList();
                    FilterProject.Items.Clear();
                    FilterProject.Items.Add(new ComboBoxItem { Content = "All Projects", Tag = null });
                    foreach (var p in projects)
                        FilterProject.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Id });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadProjectFilter Error: {ex.Message}");
            }
        }

        private void LoadReports()
        {
            if (ReportsGrid == null) return;

            using (var conn = DatabaseService.GetConnection())
            {
                var whereClauses = new List<string>();
                var parameters = new DynamicParameters();
                
                parameters.Add("Limit", _pageSize);
                parameters.Add("Offset", (_currentPage - 1) * _pageSize);

                if (!SessionManager.IsAdmin)
                {
                    whereClauses.Add("wr.UserId = @UserId");
                    parameters.Add("UserId", SessionManager.CurrentUser.Id);
                }

                if (FilterProject?.SelectedItem is ComboBoxItem pi && pi.Tag is int pid)
                {
                    whereClauses.Add("wr.ProjectId = @Pid");
                    parameters.Add("Pid", pid);
                }

                if (!string.IsNullOrWhiteSpace(FilterEmployeeName?.Text) && SessionManager.IsAdmin)
                {
                    whereClauses.Add("u.FullName ILIKE @Emp");
                    parameters.Add("Emp", $"%{FilterEmployeeName.Text.Trim()}%");
                }

                if (FilterDateFrom?.SelectedDate.HasValue == true)
                {
                    whereClauses.Add("wr.SubmissionDate::date >= @From");
                    parameters.Add("From", FilterDateFrom.SelectedDate.Value.Date);
                }

                if (FilterDateTo?.SelectedDate.HasValue == true)
                {
                    whereClauses.Add("wr.SubmissionDate::date <= @To");
                    parameters.Add("To", FilterDateTo.SelectedDate.Value.Date);
                }

                if (!string.IsNullOrEmpty(_globalSearchQuery))
                {
                    whereClauses.Add("(wr.ObjectId ILIKE @Search OR u.FullName ILIKE @Search OR p.Name ILIKE @Search)");
                    parameters.Add("Search", $"%{_globalSearchQuery}%");
                }

                string where = whereClauses.Any() ? "WHERE " + string.Join(" AND ", whereClauses) : "";

                string sql = $@"
                    SELECT wr.*, u.FullName AS EmployeeName, p.Name AS ProjectName 
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    JOIN Projects p ON p.Id = wr.ProjectId
                    {where}
                    ORDER BY wr.SubmissionDate DESC
                    LIMIT @Limit OFFSET @Offset";

                string countSql = $@"
                    SELECT COUNT(*) 
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    JOIN Projects p ON p.Id = wr.ProjectId
                    {where}";

                _allReports = conn.Query<WorkReport>(sql, parameters).AsList();
                _totalCount = (int)conn.ExecuteScalar<long>(countSql, parameters);

                // Batch load items for the reports on this page
                if (_allReports.Any())
                {
                    var reportIds = _allReports.Select(r => r.Id).ToArray(); // Array required for Npgsql ANY()
                    var itemsSql = @"
                        SELECT wri.*, pf.FieldLabel
                        FROM WorkReportItems wri
                        JOIN ProjectFields pf ON pf.Id = wri.FieldId
                        WHERE wri.WorkReportId = ANY(@Ids)";
                    
                    var allItems = conn.Query<WorkReportItem>(itemsSql, new { Ids = reportIds }).ToList();
                    
                    foreach (var report in _allReports)
                    {
                        report.Items = allItems.Where(i => i.WorkReportId == report.Id).ToList();
                    }
                }

                ReportsGrid.ItemsSource = _allReports;
                UpdatePaginationUI();
            }
            CalculateSummaries();
        }

        private void CalculateSummaries()
        {
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var today = DateTime.Today;
                    var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
                    if (today.DayOfWeek == DayOfWeek.Sunday) weekStart = weekStart.AddDays(-7);
                    var monthStart = new DateTime(today.Year, today.Month, 1);

                    var parameters = new DynamicParameters();
                    parameters.Add("Today", today);
                    parameters.Add("WeekStart", weekStart);
                    parameters.Add("MonthStart", monthStart);

                    string filterPart = "";
                    if (!SessionManager.IsAdmin)
                    {
                        filterPart = "AND wr.UserId = @UserId";
                        parameters.Add("UserId", SessionManager.CurrentUser.Id);
                    }
                    
                    if (FilterProject?.SelectedItem is ComboBoxItem pi && pi.Tag is int pid)
                    {
                        filterPart += " AND wr.ProjectId = @Pid";
                        parameters.Add("Pid", pid);
                    }

                    if (!string.IsNullOrWhiteSpace(FilterEmployeeName?.Text) && SessionManager.IsAdmin)
                    {
                        filterPart += " AND u.FullName ILIKE @Emp";
                        parameters.Add("Emp", $"%{FilterEmployeeName.Text.Trim()}%");
                    }

                    string query = $@"
                        WITH ReportItemsAgg AS (
                            SELECT 
                                wr.Id,
                                wr.SubmissionDate,
                                wr.BillingAmount,
                                SUM(CASE WHEN pf.FieldLabel ILIKE '%page%' AND wri.Value ~ '^-?[0-9.]+$' THEN wri.Value::numeric ELSE 0 END) as Pages,
                                SUM(CASE WHEN pf.FieldLabel ILIKE '%char%' AND wri.Value ~ '^-?[0-9.]+$' THEN wri.Value::numeric ELSE 0 END) as Chars
                            FROM WorkReports wr
                            LEFT JOIN WorkReportItems wri ON wri.WorkReportId = wr.Id
                            LEFT JOIN ProjectFields pf ON pf.Id = wri.FieldId
                            LEFT JOIN Users u ON u.Id = wr.UserId
                            WHERE 1=1 {filterPart}
                            GROUP BY wr.Id, wr.SubmissionDate, wr.BillingAmount
                        )
                        SELECT 
                            -- Today
                            COUNT(CASE WHEN SubmissionDate::date = @Today THEN 1 END) as TodayObjs,
                            SUM(CASE WHEN SubmissionDate::date = @Today THEN Pages ELSE 0 END) as TodayPages,
                            SUM(CASE WHEN SubmissionDate::date = @Today THEN Chars ELSE 0 END) as TodayChars,
                            SUM(CASE WHEN SubmissionDate::date = @Today THEN BillingAmount ELSE 0 END) as TodayAmt,
                            
                            -- This Week
                            COUNT(CASE WHEN SubmissionDate::date >= @WeekStart THEN 1 END) as WeekObjs,
                            SUM(CASE WHEN SubmissionDate::date >= @WeekStart THEN Pages ELSE 0 END) as WeekPages,
                            SUM(CASE WHEN SubmissionDate::date >= @WeekStart THEN Chars ELSE 0 END) as WeekChars,
                            SUM(CASE WHEN SubmissionDate::date >= @WeekStart THEN BillingAmount ELSE 0 END) as WeekAmt,

                            -- This Month
                            COUNT(CASE WHEN SubmissionDate::date >= @MonthStart THEN 1 END) as MonthObjs,
                            SUM(CASE WHEN SubmissionDate::date >= @MonthStart THEN Pages ELSE 0 END) as MonthPages,
                            SUM(CASE WHEN SubmissionDate::date >= @MonthStart THEN Chars ELSE 0 END) as MonthChars,
                            SUM(CASE WHEN SubmissionDate::date >= @MonthStart THEN BillingAmount ELSE 0 END) as MonthAmt
                        FROM ReportItemsAgg";

                    var result = conn.QueryFirstOrDefault(query, parameters);

                    if (result != null)
                    {
                        // Note: PostgreSQL returns lowercase names for unquoted aliases
                        TodayKpiText.Text = $"Objects: {result.todayobjs} | Pages: {result.todaypages ?? 0} | Chars: {result.todaychars ?? 0}\nAmount: ₹{Convert.ToDecimal(result.todayamt ?? 0):F2}";
                        WeekKpiText.Text = $"Objects: {result.weekobjs} | Pages: {result.weekpages ?? 0} | Chars: {result.weekchars ?? 0}\nAmount: ₹{Convert.ToDecimal(result.weekamt ?? 0):F2}";
                        MonthKpiText.Text = $"Objects: {result.monthobjs} | Pages: {result.monthpages ?? 0} | Chars: {result.monthchars ?? 0}\nAmount: ₹{Convert.ToDecimal(result.monthamt ?? 0):F2}";
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("crash.log", $"[{DateTime.Now}] CalculateSummaries Error: {ex.Message}\n{ex.StackTrace}\n\n");
                Debug.WriteLine($"CalculateSummaries Error: {ex.Message}");
            }
        }

        private void UpdatePaginationUI()
        {
            if (ReportsGrid == null || PageInfoText == null || PrevPageBtn == null || NextPageBtn == null) return;
            
            int totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
            if (totalPages == 0) totalPages = 1;

            PageInfoText.Text = $"Page {_currentPage} of {totalPages} (Total: {_totalCount})";
            PrevPageBtn.IsEnabled = _currentPage > 1;
            NextPageBtn.IsEnabled = _currentPage < totalPages;
        }

        public void Search(string query)
        {
            _globalSearchQuery = query?.Trim() ?? "";
            _currentPage = 1;
            LoadReports();
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (FilterEmployeeName == null || FilterProject == null || FilterDateFrom == null || FilterDateTo == null) return;
            _currentPage = 1;
            LoadReports();
        }

        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            _currentPage = 1;
            LoadReports();
        }

        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (PageSizeCombo == null || !IsLoaded) return;
            if (PageSizeCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString(), out int size))
            {
                _pageSize = size;
                _currentPage = 1;
                LoadReports();
            }
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                LoadReports();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
            if (_currentPage < totalPages)
            {
                _currentPage++;
                LoadReports();
            }
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterProject.SelectedIndex = 0;
            FilterEmployeeName.Text = "";
            FilterDateFrom.SelectedDate = null;
            FilterDateTo.SelectedDate = null;
            _globalSearchQuery = "";
            _currentPage = 1;
            LoadReports();
        }

        private void AddReport_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new WorkReportDialog { Owner = mainWindow };
            if (dialog.ShowDialog() == true)
                LoadReports();
        }

        private void PasteTable_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PasteWorkReportsDialog();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                LoadReports();
            }
        }

        private void ImportReports_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new ImportWorkReportsDialog { Owner = mainWindow };
            if (dialog.ShowDialog() == true)
                LoadReports();
        }

        private void EditReport_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var report = _allReports.FirstOrDefault(r => r.Id == id);
                if (report != null)
                {
                    var mainWindow = Window.GetWindow(this);
                    var dialog = new WorkReportDialog(report) { Owner = mainWindow };
                    if (dialog.ShowDialog() == true)
                        LoadReports();
                }
            }
        }

        private void DeleteReport_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                if (MessageBox.Show("Delete this report?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        conn.Execute("DELETE FROM WorkReportItems WHERE WorkReportId=@Id", new { Id = id });
                        conn.Execute("DELETE FROM WorkReports WHERE Id=@Id", new { Id = id });
                    }
                    LoadReports();
                }
            }
        }

        private List<WorkReport> LoadAllReportsForExport()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var whereClauses = new List<string>();
                var parameters = new DynamicParameters();

                if (!SessionManager.IsAdmin)
                {
                    whereClauses.Add("wr.UserId = @UserId");
                    parameters.Add("UserId", SessionManager.CurrentUser.Id);
                }
                if (FilterProject?.SelectedItem is ComboBoxItem pi && pi.Tag is int pid)
                {
                    whereClauses.Add("wr.ProjectId = @Pid");
                    parameters.Add("Pid", pid);
                }
                if (!string.IsNullOrWhiteSpace(FilterEmployeeName?.Text) && SessionManager.IsAdmin)
                {
                    whereClauses.Add("u.FullName ILIKE @Emp");
                    parameters.Add("Emp", $"%{FilterEmployeeName.Text.Trim()}%");
                }
                if (FilterDateFrom?.SelectedDate.HasValue == true)
                {
                    whereClauses.Add("wr.SubmissionDate::date >= @From");
                    parameters.Add("From", FilterDateFrom.SelectedDate.Value.Date);
                }
                if (FilterDateTo?.SelectedDate.HasValue == true)
                {
                    whereClauses.Add("wr.SubmissionDate::date <= @To");
                    parameters.Add("To", FilterDateTo.SelectedDate.Value.Date);
                }
                if (!string.IsNullOrEmpty(_globalSearchQuery))
                {
                    whereClauses.Add("(wr.ObjectId ILIKE @Search OR u.FullName ILIKE @Search OR p.Name ILIKE @Search)");
                    parameters.Add("Search", $"%{_globalSearchQuery}%");
                }

                string where = whereClauses.Any() ? "WHERE " + string.Join(" AND ", whereClauses) : "";
                string sql = $@"
                    SELECT wr.*, u.FullName AS EmployeeName, p.Name AS ProjectName 
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    JOIN Projects p ON p.Id = wr.ProjectId
                    {where}
                    ORDER BY wr.SubmissionDate DESC";

                var reports = conn.Query<WorkReport>(sql, parameters).AsList();

                if (reports.Any())
                {
                    var ids = reports.Select(r => r.Id).ToArray();
                    var allItems = conn.Query<WorkReportItem>(@"
                        SELECT wri.*, pf.FieldLabel
                        FROM WorkReportItems wri
                        JOIN ProjectFields pf ON pf.Id = wri.FieldId
                        WHERE wri.WorkReportId = ANY(@Ids)", new { Ids = ids }).ToList();
                    foreach (var r in reports)
                        r.Items = allItems.Where(i => i.WorkReportId == r.Id).ToList();
                }
                return reports;
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "CSV Files|*.csv", FileName = $"WorkReports_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;

            var rows = LoadAllReportsForExport();
            using (var sw = new StreamWriter(dlg.FileName, false, System.Text.Encoding.UTF8))
            {
                sw.WriteLine("ID,Employee,Project,Object ID,Pages,Characters,Billing Amount,Status,Date");
                foreach (var r in rows)
                    sw.WriteLine($"{r.Id},{r.EmployeeName},{r.ProjectName},{r.ObjectId},{r.PagesCount},{r.CharacterCount},₹{r.BillingAmount:F2},{r.Status},{r.SubmissionDate:dd/MM/yyyy}");
            }
            MessageBox.Show($"Exported {rows.Count} records successfully.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportSummaryPdf_Click(object sender, RoutedEventArgs e)
        {
            var reports = LoadAllReportsForExport();
            if (!reports.Any()) { MessageBox.Show("No data to export.", "Empty", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var dlg = new SaveFileDialog { Filter = "PDF Files|*.pdf", FileName = $"WorkReport_Summary_{DateTime.Now:yyyyMMdd}.pdf" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var service = new ReportingService();
                service.ExportWorkReportSummaryPdf(reports, dlg.FileName);
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to export PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportDetailedPdf_Click(object sender, RoutedEventArgs e)
        {
            var reports = ReportsGrid.SelectedItems.Cast<WorkReport>().ToList();
            if (!reports.Any()) 
                reports = (ReportsGrid.ItemsSource as IEnumerable<WorkReport>)?.ToList() ?? _allReports;
            
            if (!reports.Any()) { MessageBox.Show("No data to export.", "Empty", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var dlg = new SaveFileDialog { Filter = "PDF Files|*.pdf", FileName = $"WorkReport_Detailed_{DateTime.Now:yyyyMMdd}.pdf" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                // We need to ensure dynamic items are loaded for each report if they aren't already
                using (var conn = DatabaseService.GetConnection())
                {
                    foreach (var report in reports)
                    {
                        if (report.Items == null || !report.Items.Any())
                        {
                            report.Items = conn.Query<WorkReportItem>(@"
                                SELECT wri.*, pf.FieldLabel 
                                FROM WorkReportItems wri
                                JOIN ProjectFields pf ON pf.Id = wri.FieldId
                                WHERE wri.WorkReportId = @Id", new { Id = report.Id }).AsList();
                        }
                    }
                }

                var service = new ReportingService();
                service.ExportWorkReportDetailedPdf(reports, dlg.FileName);
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to export Detailed PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewAttachment_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is string path && !string.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                    catch (Exception ex) { MessageBox.Show("Could not open file: " + ex.Message); }
                }
                else
                {
                    MessageBox.Show("Attachment file not found. It may have been moved or deleted.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
