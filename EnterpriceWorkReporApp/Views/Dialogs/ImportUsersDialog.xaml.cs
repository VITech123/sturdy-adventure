using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using ExcelDataReader;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ImportUsersDialog : Window
    {
        private DataTable _mainDataTable = new();
        private Dictionary<string, string> _columnMapping = new();

        public ImportUsersDialog()
        {
            InitializeComponent();
            SetupEmptyDataTable();
        }

        private void SetupEmptyDataTable()
        {
            _mainDataTable = new DataTable();
            _mainDataTable.Columns.Add("Username");
            _mainDataTable.Columns.Add("FullName");
            _mainDataTable.Columns.Add("Password");
            _mainDataTable.Columns.Add("Role");
            PreviewGrid.ItemsSource = _mainDataTable.DefaultView;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel & CSV Files|*.xlsx;*.xls;*.csv|All Files|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                FilePathBox.Text = Path.GetFileName(dlg.FileName);
                LoadFile(dlg.FileName);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                _mainDataTable.Rows.Clear();
                _mainDataTable.Columns.Clear();
                SetupEmptyDataTable();

                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var ds = reader.AsDataSet(new ExcelDataSetConfiguration { ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true } });
                    if (ds.Tables.Count > 0)
                    {
                        var table = ds.Tables[0];
                        foreach (DataColumn col in table.Columns)
                            if (!_mainDataTable.Columns.Contains(col.ColumnName)) _mainDataTable.Columns.Add(col.ColumnName);
                        
                        foreach (DataRow row in table.Rows)
                        {
                            var newRow = _mainDataTable.NewRow();
                            foreach (DataColumn col in table.Columns) newRow[col.ColumnName] = row[col.ColumnName];
                            _mainDataTable.Rows.Add(newRow);
                        }
                    }
                }
                RefreshMapping();
                UpdateStats();
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void RefreshMapping()
        {
            MappingPanel.Children.Clear();
            _columnMapping.Clear();
            var coreFields = new[] { "Username", "FullName", "Password", "Role" };
            var fileCols = _mainDataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

            foreach (var field in coreFields)
            {
                var panel = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                panel.Children.Add(new TextBlock { Text = field, Width = 120, VerticalAlignment = VerticalAlignment.Center });
                var combo = new ComboBox { Style = (Style)FindResource("ModernComboBox") };
                combo.Items.Add("--- Select Column ---");
                foreach (var col in fileCols) combo.Items.Add(col);

                var match = fileCols.FirstOrDefault(c => c.Equals(field, StringComparison.OrdinalIgnoreCase));
                if (match != null) combo.SelectedItem = match;
                else if (field == "FullName") combo.SelectedItem = fileCols.FirstOrDefault(c => c.Contains("Name"));

                combo.SelectionChanged += (s, e) => {
                    _columnMapping[field] = combo.SelectedIndex > 0 ? combo.SelectedItem.ToString() : null;
                };
                if (combo.SelectedIndex > 0) _columnMapping[field] = combo.SelectedItem.ToString();

                panel.Children.Add(combo);
                MappingPanel.Children.Add(panel);
            }
        }

        private void UpdateStats() => GridStatsText.Text = $"Rows: {_mainDataTable.Rows.Count}";

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text)) return;
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split('\t');
                    var row = _mainDataTable.NewRow();
                    for (int i = 0; i < parts.Length && i < _mainDataTable.Columns.Count; i++) row[i] = parts[i];
                    _mainDataTable.Rows.Add(row);
                }
                UpdateStats();
            }
            catch { }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (_mainDataTable.Rows.Count == 0) return;
            try
            {
                ImportBtn.IsEnabled = false;
                int created = 0, exists = 0;

                using (var conn = DatabaseService.GetConnection())
                {
                    var rows = _mainDataTable.DefaultView.Cast<DataRowView>().ToList();
                    foreach (var rv in rows)
                    {
                        var row = rv.Row;
                        string username = GetVal(row, "Username")?.Trim();
                        string fullName = GetVal(row, "FullName")?.Trim();
                        string password = GetVal(row, "Password")?.Trim() ?? "Welcome@123";
                        string role = GetVal(row, "Role")?.Trim() ?? "Employee";

                        if (string.IsNullOrEmpty(username)) continue;

                        var existing = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Users WHERE Username=@U", new { U = username });
                        if (existing > 0) { exists++; continue; }

                        string hash = AuthService.HashPassword(password);
                        conn.Execute("INSERT INTO Users (Username, FullName, PasswordHash, Role, IsActive) VALUES (@U, @FN, @H, @R, 1)",
                            new { U = username, FN = fullName, H = hash, R = role });
                        created++;
                    }
                }
                MessageBox.Show($"Done! {created} users created. {exists} already existed.");
                DialogResult = true;
            }
            catch (Exception ex) { MessageBox.Show("Failed: " + ex.Message); }
            finally { ImportBtn.IsEnabled = true; }
        }

        private string GetVal(DataRow row, string field)
        {
            if (_columnMapping.TryGetValue(field, out var col) && row.Table.Columns.Contains(col))
                return row[col]?.ToString();
            return null;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
