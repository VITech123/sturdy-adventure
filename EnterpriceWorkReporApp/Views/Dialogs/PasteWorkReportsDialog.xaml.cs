using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class PasteWorkReportsDialog : Window
    {
        private DataTable _dataTable;

        public PasteWorkReportsDialog()
        {
            InitializeComponent();
            LoadProjects();
        }

        private void LoadProjects()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var projects = conn.Query<Project>("SELECT * FROM Projects WHERE IsActive=1 ORDER BY Name").ToList();
                ProjectCombo.ItemsSource = projects;
                if (projects.Any()) ProjectCombo.SelectedIndex = 0;
            }
        }

        private void PasteInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ParsePaste();
        }

        private void ParsePaste()
        {
            string input = PasteInput.Text;
            if (string.IsNullOrWhiteSpace(input))
            {
                PreviewGrid.ItemsSource = null;
                SaveBtn.IsEnabled = false;
                return;
            }

            try
            {
                var rows = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (rows.Length == 0) return;

                _dataTable = new DataTable();
                var headers = rows[0].Split('\t');
                
                // If only 1 column and it contains comma, try CSV parsing
                if (headers.Length == 1 && rows[0].Contains(","))
                    headers = rows[0].Split(',');

                foreach (var header in headers)
                {
                    string h = string.IsNullOrWhiteSpace(header) ? "Column" + (_dataTable.Columns.Count + 1) : header.Trim();
                    if (_dataTable.Columns.Contains(h)) h += "_" + _dataTable.Columns.Count;
                    _dataTable.Columns.Add(h);
                }

                for (int i = 1; i < rows.Length; i++)
                {
                    var values = rows[i].Split(headers.Length > 1 && !rows[0].Contains(",") ? '\t' : ',');
                    var dr = _dataTable.NewRow();
                    for (int j = 0; j < Math.Min(values.Length, _dataTable.Columns.Count); j++)
                    {
                        dr[j] = values[j].Trim();
                    }
                    _dataTable.Rows.Add(dr);
                }

                PreviewGrid.ItemsSource = _dataTable.DefaultView;
                SaveBtn.IsEnabled = _dataTable.Rows.Count > 0;
            }
            catch
            {
                // Silently fail parsing if format is weird
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectCombo.SelectedValue == null)
            {
                MessageBox.Show("Please select a project.");
                return;
            }

            int projectId = (int)ProjectCombo.SelectedValue;
            int imported = 0;
            int skipped = 0;
            int errors = 0;
            var duplicateNotes = new List<string>();
            var seenObjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Find Object ID and Amount columns
            string objCol = FindColumn(_dataTable, "Object ID", "ObjectId", "Object_ID", "ID");
            string amtCol = FindColumn(_dataTable, "Amount", "Price", "Billing Amount", "Cost");

            if (string.IsNullOrEmpty(objCol))
            {
                MessageBox.Show("Could not identify 'Object ID' column in the pasted data.", "Mapping Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var conn = DatabaseService.GetConnection())
            {
                foreach (DataRow row in _dataTable.Rows)
                {
                    try
                    {
                        string objectId = row[objCol]?.ToString();
                        if (string.IsNullOrWhiteSpace(objectId)) continue;
                        if (seenObjectIds.Contains(objectId))
                        {
                            duplicateNotes.Add($"{objectId}: duplicate in pasted data");
                            skipped++;
                            continue;
                        }
                        seenObjectIds.Add(objectId);

                        bool existing = conn.ExecuteScalar<long>(
                            "SELECT COUNT(*) FROM WorkReports WHERE ProjectId=@P AND ObjectId=@O",
                            new { P = projectId, O = objectId }) > 0;

                        if (existing)
                        {
                            duplicateNotes.Add($"{objectId}: skipped because record already exists in the system.");
                            skipped++;
                            continue;
                        }

                        decimal amount = 0;
                        if (!string.IsNullOrEmpty(amtCol))
                        {
                            string rawAmt = row[amtCol]?.ToString().Replace("₹", "").Replace("$", "").Trim();
                            decimal.TryParse(rawAmt, out amount);
                        }

                        conn.Execute(@"
                            INSERT INTO WorkReports (ProjectId, UserId, ObjectId, BillingAmount, SubmissionDate)
                            VALUES (@P, @U, @O, @A, @D)",
                            new {
                                P = projectId,
                                U = SessionManager.CurrentUser.Id,
                                O = objectId,
                                A = (float)amount,
                                D = DateTime.Now
                            });
                        imported++;
                    }
                    catch { errors++; }
                }
            }

            var summary = $"Successfully imported {imported} records. Skipped duplicates: {skipped}. Errors: {errors}.";
            if (duplicateNotes.Any())
            {
                summary += "\n\nDuplicates:\n" + string.Join("\n", duplicateNotes.Take(20));
                if (duplicateNotes.Count > 20)
                    summary += $"\n...and {duplicateNotes.Count - 20} more duplicates.";
            }

            MessageBox.Show(summary, "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            this.DialogResult = true;
            this.Close();
        }

        private string FindColumn(DataTable dt, params string[] names)
        {
            foreach (var name in names)
            {
                foreach (DataColumn col in dt.Columns)
                {
                    if (col.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                        col.ColumnName.Replace(" ", "").Replace("_", "").Equals(name.Replace(" ", "").Replace("_", ""), StringComparison.OrdinalIgnoreCase))
                    {
                        return col.ColumnName;
                    }
                }
            }
            return null;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
