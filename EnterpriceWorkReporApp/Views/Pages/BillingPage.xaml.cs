using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using Microsoft.Win32;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class BillingPage : Page
    {
        private List<WorkReport> _allRecords = new();

        public BillingPage()
        {
            InitializeComponent();
            
            // Hide employee filter for non-admin users
            if (!SessionManager.IsAdmin)
            {
                FilterEmployee.Visibility = System.Windows.Visibility.Collapsed;
            }
            
            LoadFilters();
            LoadData();
        }

        private void LoadFilters()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                FilterEmployee.Items.Clear();
                FilterEmployee.Items.Add(new ComboBoxItem { Content = "All Employees", IsSelected = true });
                foreach (var u in conn.Query<User>("SELECT * FROM Users WHERE Role!='Administrator' AND IsActive=1 ORDER BY FullName"))
                    FilterEmployee.Items.Add(new ComboBoxItem { Content = u.FullName, Tag = u.Id });

                FilterProject.Items.Clear();
                FilterProject.Items.Add(new ComboBoxItem { Content = "All Projects", IsSelected = true });
                foreach (var p in conn.Query<Project>("SELECT * FROM Projects ORDER BY Name"))
                    FilterProject.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Id });
            }
        }

        private void LoadData()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                // Regular users can only see their own billing
                string query = SessionManager.IsAdmin ? @"
                    SELECT wr.*, u.FullName AS EmployeeName, p.Name AS ProjectName 
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    JOIN Projects p ON p.Id = wr.ProjectId
                    ORDER BY wr.SubmissionDate DESC" : @"
                    SELECT wr.*, u.FullName AS EmployeeName, p.Name AS ProjectName 
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    JOIN Projects p ON p.Id = wr.ProjectId
                    WHERE wr.UserId = @UserId
                    ORDER BY wr.SubmissionDate DESC";
                
                if (SessionManager.IsAdmin)
                {
                    _allRecords = conn.Query<WorkReport>(query).AsList();
                }
                else
                {
                    _allRecords = conn.Query<WorkReport>(query, new { UserId = SessionManager.CurrentUser.Id }).AsList();
                }

                TotalBillingText.Text = $"₹{_allRecords.Sum(r => r.BillingAmount):F2}";

                var thisMonth = _allRecords.Where(r => r.SubmissionDate.Month == DateTime.Now.Month && r.SubmissionDate.Year == DateTime.Now.Year);
                MonthBillingText.Text = $"₹{thisMonth.Sum(r => r.BillingAmount):F2}";

                var today = _allRecords.Where(r => r.SubmissionDate.Date == DateTime.Today);
                TodayBillingText.Text = $"₹{today.Sum(r => r.BillingAmount):F2}";
            }
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (FilterEmployee == null || FilterProject == null || FilterFrom == null || FilterTo == null || BillingGrid == null) return;
            
            var filtered = _allRecords.AsEnumerable();
            if (FilterEmployee.SelectedItem is ComboBoxItem ei && ei.Tag is int empId)
                filtered = filtered.Where(r => r.UserId == empId);
            if (FilterProject.SelectedItem is ComboBoxItem pi && pi.Tag is int projId)
                filtered = filtered.Where(r => r.ProjectId == projId);
            if (FilterFrom.SelectedDate.HasValue)
                filtered = filtered.Where(r => r.SubmissionDate.Date >= FilterFrom.SelectedDate.Value.Date);
            if (FilterTo.SelectedDate.HasValue)
                filtered = filtered.Where(r => r.SubmissionDate.Date <= FilterTo.SelectedDate.Value.Date);
            BillingGrid.ItemsSource = filtered.ToList();
        }

        private void Filter_Changed(object sender, EventArgs e) 
        {
            if (FilterEmployee == null) return;
            ApplyFilters();
        }
        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterEmployee.SelectedIndex = 0;
            FilterProject.SelectedIndex = 0;
            FilterFrom.SelectedDate = null;
            FilterTo.SelectedDate = null;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "CSV Files|*.csv", FileName = $"Billing_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;
            var rows = BillingGrid.ItemsSource as IEnumerable<WorkReport> ?? _allRecords;
            using (var sw = new StreamWriter(dlg.FileName))
            {
                sw.WriteLine("Employee,Project,Object ID,Billing Amount,Date");
                foreach (var r in rows)
                    sw.WriteLine($"{r.EmployeeName},{r.ProjectName},{r.ObjectId},₹{r.BillingAmount:F2},{r.SubmissionDate:dd/MM/yyyy}");
            }
            MessageBox.Show("Exported successfully.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "HTML Files|*.html", FileName = $"Billing_{DateTime.Now:yyyyMMdd}.html" };
            if (dlg.ShowDialog() != true) return;
            var rows = BillingGrid.ItemsSource as IEnumerable<WorkReport> ?? _allRecords;
            ExportService.ExportBillingToHtml(rows, dlg.FileName);
            MessageBox.Show("Report exported successfully. Open the file in a web browser and use Print > Save as PDF to generate a PDF.", 
                "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
