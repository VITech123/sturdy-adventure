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
    public partial class AttendanceDialog : Window
    {
        private readonly Attendance _record;
        private readonly bool _isEdit;
        private List<User> _users = new();

        public AttendanceDialog(Attendance existing = null)
        {
            InitializeComponent();
            _record = existing ?? new Attendance { Date = DateTime.Today };
            _isEdit = existing != null;
            AttendanceDate.SelectedDate = _record.Date;

            using (var conn = DatabaseService.GetConnection())
                _users = conn.Query<User>("SELECT * FROM Users WHERE Role!='Administrator' AND IsActive=1 ORDER BY FullName").AsList();

            foreach (var u in _users)
                EmployeeCombo.Items.Add(new ComboBoxItem { Content = u.FullName, Tag = u.Id });

            if (_isEdit)
            {
                foreach (ComboBoxItem item in EmployeeCombo.Items)
                    if ((int)item.Tag == _record.UserId) { EmployeeCombo.SelectedItem = item; break; }
                foreach (ComboBoxItem item in StatusCombo.Items)
                    if (item.Content.ToString() == _record.Status) { StatusCombo.SelectedItem = item; break; }
                RemarksBox.Text = _record.Remarks;
            }
            else if (_users.Any())
                EmployeeCombo.SelectedIndex = 0;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeeCombo.SelectedItem is not ComboBoxItem emp) return;
            int userId = (int)emp.Tag;
            DateTime date = AttendanceDate.SelectedDate ?? DateTime.Today;
            string status = (StatusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Present";

            using (var conn = DatabaseService.GetConnection())
            {
                if (_isEdit)
                    conn.Execute("UPDATE Attendance SET UserId=@U, Date=@D, Status=@S, Remarks=@R WHERE Id=@Id",
                        new { U = userId, D = date, S = status, R = RemarksBox.Text.Trim(), Id = _record.Id });
                else
                    conn.Execute("INSERT INTO Attendance (UserId, Date, Status, Remarks) VALUES (@U, @D, @S, @R)",
                        new { U = userId, D = date, S = status, R = RemarksBox.Text.Trim() });
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
