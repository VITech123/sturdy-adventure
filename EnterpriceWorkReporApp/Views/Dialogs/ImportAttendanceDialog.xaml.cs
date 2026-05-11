using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using ExcelDataReader;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ImportAttendanceDialog : Window
    {
        private DataTable _mainDataTable = new();
        private List<User> _allUsers = new();
        private Dictionary<string, string> _columnMapping = new();
        private FuzzyNameMatcher _nameMatcher = new();

        public ImportAttendanceDialog()
        {
            InitializeComponent();
            LoadUsers();
            InitializeFuzzyMatcher();
            SetupEmptyDataTable();
        }

        private void InitializeFuzzyMatcher()
        {
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var settings = conn.QueryFirstOrDefault<CompanySettings>("SELECT MasterNamesPath FROM CompanySettings WHERE Id=1");
                    if (settings != null && !string.IsNullOrEmpty(settings.MasterNamesPath))
                        _nameMatcher.LoadMasterNames(settings.MasterNamesPath);
                }
            }
            catch { }
        }

        private void LoadUsers()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                _allUsers = conn.Query<User>("SELECT * FROM Users WHERE IsActive=1").ToList();
            }
            FilterUserCombo.Items.Clear();
            FilterUserCombo.Items.Add(new ComboBoxItem { Content = "All Users", Tag = null });
            foreach (var u in _allUsers.OrderBy(u => u.FullName))
                FilterUserCombo.Items.Add(new ComboBoxItem { Content = u.FullName, Tag = u.Id });
            FilterUserCombo.SelectedIndex = 0;
        }

        private void SetupEmptyDataTable()
        {
            _mainDataTable = new DataTable();
            _mainDataTable.Columns.Add("Date");
            _mainDataTable.Columns.Add("Name");
            _mainDataTable.Columns.Add("ClockIn");
            _mainDataTable.Columns.Add("ClockOut");
            _mainDataTable.Columns.Add("Status");
            _mainDataTable.Columns.Add("Remarks");
            PreviewGrid.ItemsSource = _mainDataTable.DefaultView;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel & CSV Files|*.xlsx;*.xls;*.csv|All Files|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                FilePathBox.Text = string.Join(", ", dlg.FileNames.Select(Path.GetFileName));
                LoadMultipleFiles(dlg.FileNames);
            }
        }

        private void LoadMultipleFiles(string[] paths)
        {
            _mainDataTable.Rows.Clear();
            _mainDataTable.Columns.Clear();
            SetupEmptyDataTable();

            foreach (var path in paths)
            {
                try
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".csv") AppendCsv(path);
                    else AppendExcel(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading {Path.GetFileName(path)}: {ex.Message}");
                }
            }
            AutoProcessData();
            RefreshMapping();
            UpdateGridStats();
        }

        private void AppendCsv(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            var lines = File.ReadAllLines(path);
            if (lines.Length < 1) return;

            var headers = Regex.Split(lines[0], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)").Select(h => h.Trim('"', ' ')).ToList();
            foreach (var h in headers) if (!_mainDataTable.Columns.Contains(h)) _mainDataTable.Columns.Add(h);

            for (int i = 1; i < lines.Length; i++)
            {
                var vals = Regex.Split(lines[i], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)").Select(v => v.Trim('"', ' ')).ToList();
                if (vals.All(string.IsNullOrWhiteSpace)) continue;
                var row = _mainDataTable.NewRow();
                for (int c = 0; c < headers.Count && c < vals.Count; c++) row[headers[c]] = vals[c];
                if (string.IsNullOrWhiteSpace(row["Name"]?.ToString())) row["Name"] = fileName;
                _mainDataTable.Rows.Add(row);
            }
        }

        private void AppendExcel(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            try
            {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                    });

                    // Read ALL sheets and combine
                    foreach (DataTable table in ds.Tables)
                    {
                        string sheetName = table.TableName;
                        Console.WriteLine($"[ImportAttendance] Reading sheet: {sheetName}");
                        
                        var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                        
                        // Ensure we have a Month column for sheet-based month extraction
                        if (!_mainDataTable.Columns.Contains("SheetSource"))
                            _mainDataTable.Columns.Add("SheetSource");
                        if (!_mainDataTable.Columns.Contains("Month"))
                            _mainDataTable.Columns.Add("Month");
                        
                        foreach (var h in headers)
                            if (!_mainDataTable.Columns.Contains(h))
                                _mainDataTable.Columns.Add(h);

                        foreach (DataRow sourceRow in table.Rows)
                        {
                            if (sourceRow.ItemArray.All(v => v == null || string.IsNullOrWhiteSpace(v.ToString()))) continue;
                            var newRow = _mainDataTable.NewRow();
                            foreach (var col in headers) newRow[col] = sourceRow[col];
                            
                            // Store sheet name for month extraction
                            newRow["SheetSource"] = sheetName;
                            
                            // Try extract month from sheet name if Month column is empty
                            if (string.IsNullOrWhiteSpace(newRow["Month"]?.ToString()))
                            {
                                // Sheet name like "January", "Jan-2025", "2025-01"
                                string monthFromSheet = ExtractMonthFromSheetName(sheetName);
                                if (!string.IsNullOrEmpty(monthFromSheet))
                                {
                                    newRow["Month"] = monthFromSheet;
                                }
                            }
                            
                            // If Name is empty, use sheet name as hint
                            if (string.IsNullOrWhiteSpace(newRow["Name"]?.ToString()))
                                newRow["Name"] = sheetName;
                                
                            _mainDataTable.Rows.Add(newRow);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Password handling...
                if (ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
                    || ex.Message.IndexOf("encrypt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var pwdDialog = new PasswordDialog();
                    if (pwdDialog.ShowDialog() == true)
                    {
                        string password = pwdDialog.Password;
                        using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            var config = new ExcelReaderConfiguration { Password = password };
                            using (var reader = ExcelReaderFactory.CreateReader(stream, config))
                            {
                                var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                                {
                                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                                });

                                foreach (DataTable table in ds.Tables)
                                {
                                    string sheetName = table.TableName;
                                    var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                                    if (!_mainDataTable.Columns.Contains("SheetSource"))
                                        _mainDataTable.Columns.Add("SheetSource");
                                    if (!_mainDataTable.Columns.Contains("Month"))
                                        _mainDataTable.Columns.Add("Month");
                                        
                                    foreach (var h in headers)
                                        if (!_mainDataTable.Columns.Contains(h))
                                            _mainDataTable.Columns.Add(h);

                                    foreach (DataRow sourceRow in table.Rows)
                                    {
                                        if (sourceRow.ItemArray.All(v => v == null || string.IsNullOrWhiteSpace(v.ToString()))) continue;
                                        var newRow = _mainDataTable.NewRow();
                                        foreach (var col in headers) newRow[col] = sourceRow[col];
                                        newRow["SheetSource"] = sheetName;
                                        if (string.IsNullOrWhiteSpace(newRow["Month"]?.ToString()))
                                        {
                                            string monthFromSheet = ExtractMonthFromSheetName(sheetName);
                                            if (!string.IsNullOrEmpty(monthFromSheet))
                                                newRow["Month"] = monthFromSheet;
                                        }
                                        if (string.IsNullOrWhiteSpace(newRow["Name"]?.ToString()))
                                            newRow["Name"] = sheetName;
                                        _mainDataTable.Rows.Add(newRow);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Password required to read encrypted Excel file.");
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        private string ExtractMonthFromSheetName(string sheetName)
        {
            if (string.IsNullOrWhiteSpace(sheetName)) return null;
            
            sheetName = sheetName.Trim();
            
            // Try parse as month name: "January", "Jan", "JAN"
            string[] monthNames = { "january", "february", "march", "april", "may", "june", 
                                   "july", "august", "september", "october", "november", "december" };
            string lower = sheetName.ToLower();
            
            for (int i = 0; i < monthNames.Length; i++)
            {
                if (lower.Contains(monthNames[i]) || lower.StartsWith(monthNames[i].Substring(0, 3)))
                {
                    // Return as year-month format assuming current year if not specified
                    var now = DateTime.Now;
                    return $"{now:yyyy}-{(i+1):D2}";
                }
            }
            
            // Try parse as "Jan-2025", "2025-01", "01/2025"
            var match = System.Text.RegularExpressions.Regex.Match(sheetName, @"(\d{4})[-/]?(\d{1,2})");
            if (match.Success && match.Groups.Count >= 3)
            {
                int year = int.Parse(match.Groups[1].Value);
                int month = int.Parse(match.Groups[2].Value);
                if (year >= 2000 && month >= 1 && month <= 12)
                    return $"{year}-{month:D2}";
            }
            
            // Try "MM/YYYY" or "MM-YYYY"
            match = System.Text.RegularExpressions.Regex.Match(sheetName, @"(\d{1,2})[-/](\d{4})");
            if (match.Success && match.Groups.Count >= 3)
            {
                int month = int.Parse(match.Groups[1].Value);
                int year = int.Parse(match.Groups[2].Value);
                if (year >= 2000 && month >= 1 && month <= 12)
                    return $"{year}-{month:D2}";
            }
            
            return null;
        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
                    || ex.Message.IndexOf("encrypt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var pwdDialog = new PasswordDialog();
                    if (pwdDialog.ShowDialog() == true)
                    {
                        string password = pwdDialog.Password;
                        using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            var config = new ExcelReaderConfiguration { Password = password };
                            using (var reader = ExcelReaderFactory.CreateReader(stream, config))
                            {
                                var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                                {
                                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                                });

                                foreach (DataTable table in ds.Tables)
                                {
                                    string sheetName = table.TableName;
                                    var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                                    foreach (var h in headers)
                                        if (!_mainDataTable.Columns.Contains(h))
                                            _mainDataTable.Columns.Add(h);

                                    foreach (DataRow sourceRow in table.Rows)
                                    {
                                        if (sourceRow.ItemArray.All(v => v == null || string.IsNullOrWhiteSpace(v.ToString()))) continue;
                                        var newRow = _mainDataTable.NewRow();
                                        foreach (var col in headers) newRow[col] = sourceRow[col];
                                        if (string.IsNullOrWhiteSpace(newRow["Name"]?.ToString()))
                                            newRow["Name"] = sheetName;
                                        _mainDataTable.Rows.Add(newRow);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Password required to read encrypted Excel file.");
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        private void AutoProcessData()
        {
            foreach (DataRow row in _mainDataTable.Rows)
            {
                string nameVal = row["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(nameVal))
                    row["Name"] = _nameMatcher.Resolve(nameVal);
            }
        }

        private void RefreshMapping()
        {
            MappingPanel.Children.Clear();
            _columnMapping.Clear();
            var coreFields = new[] { "Date", "Name", "ClockIn", "ClockOut", "Status", "Remarks" };
            var fileCols = _mainDataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

            foreach (var field in coreFields)
            {
                var panel = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                panel.Children.Add(new TextBlock { Text = field, Width = 120, VerticalAlignment = VerticalAlignment.Center });
                var combo = new ComboBox { Style = (Style)FindResource("ModernComboBox") };
                combo.Items.Add("--- Select Column ---");
                foreach (var col in fileCols) combo.Items.Add(col);

                // Auto-map if names match
                var match = fileCols.FirstOrDefault(c => c.Equals(field, StringComparison.OrdinalIgnoreCase));
                if (match != null) combo.SelectedItem = match;
                else if (field == "Name") combo.SelectedItem = fileCols.FirstOrDefault(c => c.Contains("Employee") || c.Contains("User"));
                else if (field == "ClockIn") combo.SelectedItem = fileCols.FirstOrDefault(c => c.Contains("In") || c.Contains("Start"));
                else if (field == "ClockOut") combo.SelectedItem = fileCols.FirstOrDefault(c => c.Contains("Out") || c.Contains("End"));
                else if (field == "Date") combo.SelectedItem = fileCols.FirstOrDefault(c => c.Contains("Month"));

                combo.SelectionChanged += (s, e) => {
                    _columnMapping[field] = combo.SelectedIndex > 0 ? combo.SelectedItem.ToString() : null;
                };
                if (combo.SelectedIndex > 0) _columnMapping[field] = combo.SelectedItem.ToString();

                panel.Children.Add(combo);
                MappingPanel.Children.Add(panel);
            }
        }

        private void UpdateGridStats() => GridStatsText.Text = $"Rows: {_mainDataTable.Rows.Count}";

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (_mainDataTable == null) return;
            var filter = new List<string>();
            if (!string.IsNullOrWhiteSpace(GridSearchBox.Text)) filter.Add($"(Name LIKE '%{GridSearchBox.Text}%' OR Remarks LIKE '%{GridSearchBox.Text}%')");
            if (FilterUserCombo.SelectedItem is ComboBoxItem ui && ui.Tag is int uid) filter.Add($"Name = '{ui.Content}'");
            if (FilterDatePicker.SelectedDate.HasValue) filter.Add($"Date LIKE '%{FilterDatePicker.SelectedDate.Value:yyyy-MM-dd}%'");
            _mainDataTable.DefaultView.RowFilter = string.Join(" AND ", filter);
            UpdateGridStats();
        }

        private void ClearGrid_Click(object sender, RoutedEventArgs e) { _mainDataTable.Rows.Clear(); UpdateGridStats(); }

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
                UpdateGridStats();
            }
            catch (Exception ex) { MessageBox.Show("Paste failed: " + ex.Message); }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (_mainDataTable.Rows.Count == 0) { MessageBox.Show("No data to import."); return; }
            try
            {
                ImportBtn.IsEnabled = false;
                ImportBtn.Content = "Importing...";
                int imported = 0, skipped = 0;

                using (var conn = DatabaseService.GetConnection())
                {
                    var rows = _mainDataTable.DefaultView.Cast<DataRowView>().ToList();
                    foreach (var rv in rows)
                    {
                        var row = rv.Row;
                        try
                        {
                            string name = GetMapped(row, "Name");
                            string dateStr = GetMapped(row, "Date");
                            string monthStr = GetMapped(row, "Month");
                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(monthStr)) { skipped++; continue; }

                            var user = _allUsers.FirstOrDefault(u => u.FullName.Equals(name, StringComparison.OrdinalIgnoreCase) || u.Username.Equals(name, StringComparison.OrdinalIgnoreCase));
                            if (user == null)
                                user = _allUsers.FirstOrDefault(u => u.FullName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
                            if (user == null) { skipped++; continue; }

                            DateTime date;
                            if (!TryParseAttendanceDate(dateStr, out date) && !TryParseAttendanceMonth(monthStr, out date)) { skipped++; continue; }

                            string clockIn = GetMapped(row, "ClockIn") ?? "";
                            string clockOut = GetMapped(row, "ClockOut") ?? "";
                            string status = NormalizeAttendanceStatus(GetMapped(row, "Status"));
                            if (string.IsNullOrWhiteSpace(status))
                                status = string.IsNullOrWhiteSpace(clockIn) && string.IsNullOrWhiteSpace(clockOut) ? "Absent" : "Present";
                            string remarks = GetMapped(row, "Remarks") ?? "";

                            conn.Execute(@"
                                INSERT INTO Attendance (UserId, Date, Status, ClockInTime, ClockOutTime, Remarks)
                                VALUES (@U, @D, @S, @In, @Out, @R)
                                ON CONFLICT (UserId, Date) DO UPDATE SET 
                                    Status = EXCLUDED.Status, ClockInTime = EXCLUDED.ClockInTime, 
                                    ClockOutTime = EXCLUDED.ClockOutTime, Remarks = EXCLUDED.Remarks",
                                new { U = user.Id, D = date, S = status, In = clockIn, Out = clockOut, R = remarks });
                            imported++;
                        }
                        catch { skipped++; }
                    }
                }
                MessageBox.Show($"Import complete! {imported} imported, {skipped} skipped.");
                DialogResult = true;
            }
            catch (Exception ex) { MessageBox.Show("Import failed: " + ex.Message); }
            finally { ImportBtn.IsEnabled = true; ImportBtn.Content = "🚀 Import Records"; }
        }

        private string GetMapped(DataRow row, string field)
        {
            if (_columnMapping.TryGetValue(field, out var col) && row.Table.Columns.Contains(col))
                return row[col]?.ToString();

            foreach (DataColumn c in row.Table.Columns)
            {
                if (c.ColumnName.Equals(field, StringComparison.OrdinalIgnoreCase))
                    return row[c]?.ToString();
            }
            return null;
        }

        private string NormalizeAttendanceStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            status = status.Trim().ToLowerInvariant();
            if (status.StartsWith("p")) return "Present";
            if (status.StartsWith("a")) return "Absent";
            if (status.Contains("present")) return "Present";
            if (status.Contains("absent")) return "Absent";
            if (status.Contains("leave")) return "Absent";
            return status.Length <= 3 ? (status == "yes" || status == "y" ? "Present" : status == "no" || status == "n" ? "Absent" : status) : status;
        }

        private bool TryParseAttendanceDate(string dateText, out DateTime date)
        {
            date = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(dateText)) return false;
            if (DateTime.TryParse(dateText, out date)) return true;

            var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd-MM-yyyy", "MM-dd-yyyy", "yyyyMMdd" };
            return DateTime.TryParseExact(dateText.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        private bool TryParseAttendanceMonth(string monthText, out DateTime date)
        {
            date = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(monthText)) return false;
            monthText = monthText.Trim();
            if (DateTime.TryParseExact(monthText, new[] { "yyyy-MM", "MMM yyyy", "MMMM yyyy", "MM/yyyy", "yyyy/MM" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;

            // fallback to first day of month if only numeric month/year found
            var parts = monthText.Split(new[] { '/', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int y))
            {
                if (m >= 1 && m <= 12)
                {
                    date = new DateTime(y, m, 1);
                    return true;
                }
            }

            return false;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
