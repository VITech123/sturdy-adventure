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
    public partial class LeaveDialog : Window
    {
        private List<User> _users = new();

        public LeaveDialog()
        {
            InitializeComponent();
            StartDate.SelectedDate = DateTime.Today;
            EndDate.SelectedDate = DateTime.Today;

            using (var conn = DatabaseService.GetConnection())
                _users = conn.Query<User>("SELECT * FROM Users WHERE IsActive=1 ORDER BY FullName").AsList();

            foreach (var u in _users)
                EmployeeCombo.Items.Add(new ComboBoxItem { Content = u.FullName, Tag = u.Id });

            // Default to current user and restrict for non-admins
            var cur = SessionManager.CurrentUser;
            if (cur != null)
            {
                foreach (ComboBoxItem item in EmployeeCombo.Items)
                {
                    if ((int)item.Tag == cur.Id) 
                    { 
                        EmployeeCombo.SelectedItem = item; 
                        break; 
                    }
                }
                
                // If not admin, they can only apply for themselves
                if (cur.Role != "Administrator")
                {
                    EmployeeCombo.IsEnabled = false;
                }
            }
            
            if (EmployeeCombo.SelectedIndex < 0 && _users.Any())
                EmployeeCombo.SelectedIndex = 0;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeeCombo.SelectedItem is not ComboBoxItem emp) return;
            if (!StartDate.SelectedDate.HasValue || !EndDate.SelectedDate.HasValue) return;

            if (StartDate.SelectedDate.Value > EndDate.SelectedDate.Value)
            {
                MessageBox.Show("End date must be on or after Start date.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int userId = (int)emp.Tag;
            DateTime startDate = StartDate.SelectedDate.Value;
            DateTime endDate = EndDate.SelectedDate.Value;
            string leaveType = (LeaveTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();

            using (var conn = DatabaseService.GetConnection())
            {
                // --- Overlap Detection ---
                long overlaps = conn.ExecuteScalar<long>(@"
                    SELECT COUNT(*) FROM Leaves
                    WHERE UserId = @U
                      AND Status IN ('Pending','Approved')
                      AND StartDate <= @E
                      AND EndDate   >= @S",
                    new { U = userId, S = startDate, E = endDate });

                if (overlaps > 0)
                {
                    MessageBox.Show(
                        "This employee already has a Pending or Approved leave that overlaps with the selected dates.\n" +
                        "Please choose different dates or cancel the existing request first.",
                        "Overlapping Leave", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                conn.Execute(@"INSERT INTO Leaves (UserId, LeaveType, StartDate, EndDate, Reason, Status)
                               VALUES (@U, @LT, @S, @E, @R, 'Pending')",
                    new
                    {
                        U  = userId,
                        LT = leaveType,
                        S  = startDate,
                        E  = endDate,
                        R  = ReasonBox.Text.Trim()
                    });
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
