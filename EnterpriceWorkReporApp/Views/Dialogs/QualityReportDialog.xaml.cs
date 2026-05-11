using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class QualityReportDialog : Window
    {
        private List<User> _users = new();
        private readonly QualityReport _report;
        private readonly bool _isEdit;

        public QualityReportDialog(QualityReport existing = null)
        {
            InitializeComponent();
            _report = existing ?? new QualityReport { ReportDate = DateTime.Today };
            _isEdit = existing != null;

            if (_isEdit) TitleText.Text = "Edit Quality Report";
            ReportDate.SelectedDate = _report.ReportDate;

            using (var conn = DatabaseService.GetConnection())
                _users = conn.Query<User>("SELECT * FROM Users WHERE Role!='Administrator' AND IsActive=1 ORDER BY FullName").AsList();

            foreach (var u in _users)
                EmployeeCombo.Items.Add(new ComboBoxItem { Content = u.FullName, Tag = u.Id });

            if (_isEdit)
            {
                foreach (ComboBoxItem item in EmployeeCombo.Items)
                    if ((int)item.Tag == _report.UserId) { EmployeeCombo.SelectedItem = item; break; }
                
                AccuracyBox.Text = _report.Accuracy.ToString("F2");
                ErrorRateBox.Text = _report.ErrorRate.ToString("F2");
                ReworkBox.Text = _report.ReworkCount.ToString();
                ScoreBox.Text = _report.QualityScore.ToString("F2");
                RemarksBox.Text = _report.Remarks ?? "";
            }
            else if (_users.Any()) EmployeeCombo.SelectedIndex = 0;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeeCombo.SelectedItem is not ComboBoxItem emp) { ErrorText.Text = "Select employee."; return; }
            if (!double.TryParse(AccuracyBox.Text, out double acc)) { ErrorText.Text = "Invalid accuracy."; return; }
            if (!double.TryParse(ErrorRateBox.Text, out double err)) { ErrorText.Text = "Invalid error rate."; return; }
            if (!int.TryParse(ReworkBox.Text, out int rework)) { ErrorText.Text = "Invalid rework count."; return; }
            if (!double.TryParse(ScoreBox.Text, out double score)) { ErrorText.Text = "Invalid quality score."; return; }

            DateTime date = ReportDate.SelectedDate ?? DateTime.Today;
            using (var conn = DatabaseService.GetConnection())
            {
                if (_isEdit)
                {
                    conn.Execute(@"UPDATE QualityReports SET UserId=@U, ReportDate=@D, Accuracy=@A, ErrorRate=@E, ReworkCount=@R, QualityScore=@Q, Remarks=@Rem WHERE Id=@Id",
                        new { U = (int)emp.Tag, D = date, A = acc, E = err, R = rework, Q = score, Rem = RemarksBox.Text.Trim(), Id = _report.Id });
                }
                else
                {
                    conn.Execute(@"INSERT INTO QualityReports (UserId, ReportDate, Accuracy, ErrorRate, ReworkCount, QualityScore, Remarks)
                                   VALUES (@U, @D, @A, @E, @R, @Q, @Rem)",
                        new { U = (int)emp.Tag, D = date, A = acc, E = err, R = rework, Q = score, Rem = RemarksBox.Text.Trim() });
                }
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
