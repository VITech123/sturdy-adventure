using System;
using System.Collections.Generic;
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
    public partial class QualityPage : Page
    {
        private List<QualityReport> _allReports = new();
        private string _qualityReportsPath = "";
        private double _qualityThreshold = 85.0;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalCount = 0;

        public QualityPage()
        {
            try
            {
                InitializeComponent();
                LoadQualityReportsPath();
                LoadQualityThreshold();
                
                if (PageSizeCombo != null)
                    PageSizeCombo.SelectedIndex = 1; // Default 50

                if (!SessionManager.IsAdmin)
                {
                    if (ActionsCol != null) ActionsCol.Visibility = Visibility.Collapsed;
                    if (FilterEmployeeName != null) FilterEmployeeName.Visibility = Visibility.Collapsed;
                    if (AddQualityBtn != null) AddQualityBtn.Visibility = Visibility.Collapsed;
                    if (ImportBtn != null) ImportBtn.Visibility = Visibility.Collapsed;
                    if (ReportPathBox != null) ReportPathBox.IsEnabled = false;
                    if (SavePath_Click_Btn != null) SavePath_Click_Btn.Visibility = Visibility.Collapsed; 
                }
                
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Quality Page failed to load: {ex.Message}\n\nStack Trace: {ex.StackTrace}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"QualityPage Init Error: {ex}");
            }
        }

        private void LoadQualityReportsPath()
        {
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var settings = conn.QueryFirstOrDefault<CompanySettings>(
                        "SELECT QualityReportsPath FROM CompanySettings WHERE Id = 1");
                    if (settings != null)
                    {
                        _qualityReportsPath = settings.QualityReportsPath;
                        ReportPathBox.Text = _qualityReportsPath;
                    }
                }
            }
            catch { }
        }

        private void LoadQualityThreshold()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                _qualityThreshold = conn.ExecuteScalar<double>(
                    "SELECT QualityThreshold FROM CompanySettings WHERE Id = 1");
                if (_qualityThreshold <= 0) _qualityThreshold = 85.0;
            }
        }

        private void SavePath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newPath = ReportPathBox.Text.Trim();
                using (var conn = DatabaseService.GetConnection())
                {
                    conn.Execute(
                        "UPDATE CompanySettings SET QualityReportsPath = @Path WHERE Id = 1",
                        new { Path = newPath });
                    _qualityReportsPath = newPath;
                    MessageBox.Show("Quality reports folder path saved!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving path: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadData()
        {
            if (QualityGrid == null) return;

            using (var conn = DatabaseService.GetConnection())
            {
                var whereClauses = new List<string>();
                var parameters = new DynamicParameters();
                
                parameters.Add("Limit", _pageSize);
                parameters.Add("Offset", (_currentPage - 1) * _pageSize);

                if (!SessionManager.IsAdmin)
                {
                    whereClauses.Add("qr.UserId = @UserId");
                    parameters.Add("UserId", SessionManager.CurrentUser.Id);
                }

                if (FilterEmployeeName != null && !string.IsNullOrWhiteSpace(FilterEmployeeName.Text) && SessionManager.IsAdmin)
                {
                    whereClauses.Add("u.FullName ILIKE @Emp");
                    parameters.Add("Emp", $"%{FilterEmployeeName.Text.Trim()}%");
                }

                if (FilterDateFrom?.SelectedDate.HasValue == true)
                {
                    whereClauses.Add("qr.ReportDate >= @From");
                    parameters.Add("From", FilterDateFrom.SelectedDate.Value.Date);
                }

                if (FilterDateTo?.SelectedDate.HasValue == true)
                {
                    whereClauses.Add("qr.ReportDate <= @To");
                    parameters.Add("To", FilterDateTo.SelectedDate.Value.Date);
                }

                string where = whereClauses.Any() ? "WHERE " + string.Join(" AND ", whereClauses) : "";

                string sql = $@"
                    SELECT qr.*, u.FullName AS EmployeeName
                    FROM QualityReports qr
                    JOIN Users u ON u.Id = qr.UserId
                    {where}
                    ORDER BY qr.ReportDate DESC, u.FullName
                    LIMIT @Limit OFFSET @Offset";

                string countSql = $@"
                    SELECT COUNT(*) 
                    FROM QualityReports qr
                    JOIN Users u ON u.Id = qr.UserId
                    {where}";

                _allReports = conn.Query<QualityReport>(sql, parameters).AsList();
                _totalCount = (int)conn.ExecuteScalar<long>(countSql, parameters);

                foreach (var r in _allReports)
                    r.IsBelowThreshold = r.QualityScore > 0 && r.QualityScore < _qualityThreshold;

                QualityGrid.ItemsSource = _allReports;
                UpdateGridHeaders();
                UpdatePaginationUI();
            }
        }

        private void UpdatePaginationUI()
        {
            if (QualityGrid == null || PageInfoText == null || PrevPageBtn == null || NextPageBtn == null) return;

            int totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
            if (totalPages == 0) totalPages = 1;

            PageInfoText.Text = $"Page {_currentPage} of {totalPages} (Total: {_totalCount})";
            PrevPageBtn.IsEnabled = _currentPage > 1;
            NextPageBtn.IsEnabled = _currentPage < totalPages;
        }

        private void UpdateGridHeaders()
        {
            if (QualityGrid == null) return;

            // Default headers
            string employeeHeader = "Employee";
            
            // If project specific terminology is needed, we can detect it here
            // For Gamma 142/143, they use "Vendor Name" or similar in their quality reports
            if (_allReports.Any())
            {
                // In your screenshot, it shows 'Vendor_Name'
                employeeHeader = "Vendor Name";
            }

            foreach (var col in QualityGrid.Columns)
            {
                if (col.Header?.ToString() == "Employee" || col.Header?.ToString() == "Vendor Name")
                {
                    col.Header = employeeHeader;
                }
            }
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
            if (FilterEmployeeName == null || FilterDateFrom == null || FilterDateTo == null) return;
            _currentPage = 1;
            LoadData();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterEmployeeName.Text = "";
            FilterDateFrom.SelectedDate = null;
            FilterDateTo.SelectedDate = null;
            _currentPage = 1;
            LoadData();
        }

        private void EditQuality_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var report = _allReports.FirstOrDefault(r => r.Id == id);
                if (report != null)
                {
                    var dialog = new QualityReportDialog(report);
                    if (dialog.ShowDialog() == true) LoadData();
                }
            }
        }

        private void DeleteQuality_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                if (MessageBox.Show("Delete this quality report?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        conn.Execute("DELETE FROM QualityReports WHERE Id=@Id", new { Id = id });
                        AuditService.Log("Quality Report Deleted", $"Record ID {id} deleted");
                    }
                    LoadData();
                }
            }
        }

        private void AddQuality_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new QualityReportDialog();
            if (dialog.ShowDialog() == true) LoadData();
        }

        private void ImportFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ImportQualityReportsDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void QualityGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QualityGrid.SelectedItem is QualityReport selected)
            {
                DetailTitleText.Text = $"Details for {selected.EmployeeName} - {selected.ReportDate:dd/MM/yyyy}";
                LoadDetails(selected.Id);
            }
            else
            {
                DetailTitleText.Text = "Select a record above to view details";
                DetailGrid.ItemsSource = null;
            }
        }

        private void LoadDetails(int reportId)
        {
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var details = conn.Query("SELECT * FROM QualityReportItems WHERE ReportId = @Id", new { Id = reportId }).ToList();
                    DetailGrid.ItemsSource = details;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading quality details: {ex.Message}");
            }
        }
    }
}
