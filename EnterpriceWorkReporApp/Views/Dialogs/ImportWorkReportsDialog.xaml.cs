using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using ExcelDataReader;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ImportWorkReportsDialog : Window
    {
        private List<Project> _projects = new();
        private List<ProjectField> _currentFields = new();
        private DataTable _mainDataTable = new();
        private FuzzyNameMatcher _nameMatcher = new();
        private List<User> _allUsers = new();

        public ImportWorkReportsDialog()
        {
            InitializeComponent();
            LoadProjects();
            LoadUsers();
            InitializeFuzzyMatcher();
            SetupEmptyDataTable();
        }

        private void LoadProjects()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                _projects = conn.Query<Project>("SELECT * FROM Projects WHERE IsActive=1 ORDER BY Name").AsList();
            }
            ProjectCombo.Items.Clear();
            foreach (var p in _projects)
                ProjectCombo.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Id });

            if (_projects.Any())
                ProjectCombo.SelectedIndex = 0;
        }

        private void LoadUsers()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                _allUsers = conn.Query<User>("SELECT * FROM Users WHERE IsActive=1").ToList();
                FilterUserCombo.Items.Clear();
                FilterUserCombo.Items.Add(new ComboBoxItem { Content = "All Users", Tag = null });
                foreach (var u in _allUsers.OrderBy(u => u.FullName))
                    FilterUserCombo.Items.Add(new ComboBoxItem { Content = u.FullName, Tag = u.Id });
                FilterUserCombo.SelectedIndex = 0;
            }
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

        private void SetupEmptyDataTable()
        {
            _mainDataTable = new DataTable();
            // Core columns expected by the user
            string[] coreCols = { "Date", "ManifestId", "ObjectId", "Name", "Pages", "Articles", "Characters", "Batch", "Priority", "Quality", "ShipmentFlag", "Batch2" };
            foreach (var col in coreCols)
                _mainDataTable.Columns.Add(col);
            
            PreviewGrid.ItemsSource = _mainDataTable.DefaultView;
        }

        private void Project_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectCombo.SelectedItem is ComboBoxItem item && item.Tag is int projectId)
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    _currentFields = conn.Query<ProjectField>(
                        "SELECT * FROM ProjectFields WHERE ProjectId=@Id ORDER BY SortOrder",
                        new { Id = projectId }).AsList();
                }
                RefreshMapping();
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel & CSV Files|*.xlsx;*.xls;*.csv|All Files|*.*",
                Title = "Select Work Report / Master Files",
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
            SetupEmptyDataTable(); // Reset to base structure

            foreach (var path in paths)
            {
                try
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".csv")
                        AppendCsv(path);
                    else
                        AppendExcel(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading file {Path.GetFileName(path)}:\n{ex.Message}");
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

            var originalHeaders = SplitCsvLine(lines[0]).Select(h => h.Trim('"', ' ')).ToList();
            var mappedHeaders = originalHeaders.Select(h => MapHeader(h)).ToList();
            EnsureColumnsExist(mappedHeaders);

            for (int i = 1; i < lines.Length; i++)
            {
                var vals = SplitCsvLine(lines[i]).Select(v => v.Trim('"', ' ')).ToList();
                if (vals.All(string.IsNullOrWhiteSpace)) continue;
                
                var row = _mainDataTable.NewRow();
                for (int c = 0; c < originalHeaders.Count && c < vals.Count; c++)
                {
                    string targetCol = mappedHeaders[c];
                    if (_mainDataTable.Columns.Contains(targetCol))
                        row[targetCol] = vals[c];
                }

                // If Name is empty and we have a filename hint, use it
                if (string.IsNullOrWhiteSpace(row["Name"]?.ToString()))
                    row["Name"] = fileName;

                _mainDataTable.Rows.Add(row);
            }
        }

        private void AppendExcel(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                });

                foreach (DataTable table in ds.Tables)
                {
                    var originalHeaders = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                    var mappedHeaders = originalHeaders.Select(h => MapHeader(h)).ToList();
                    EnsureColumnsExist(mappedHeaders);

                    foreach (DataRow sourceRow in table.Rows)
                    {
                        if (sourceRow.ItemArray.All(v => v == null || string.IsNullOrWhiteSpace(v.ToString()))) continue;
                        
                        var newRow = _mainDataTable.NewRow();
                        for (int i = 0; i < originalHeaders.Count; i++)
                        {
                            string targetCol = mappedHeaders[i];
                            if (_mainDataTable.Columns.Contains(targetCol))
                                newRow[targetCol] = sourceRow[originalHeaders[i]];
                        }

                        // If Name is empty and we have a filename hint, use it
                        if (string.IsNullOrWhiteSpace(newRow["Name"]?.ToString()))
                            newRow["Name"] = fileName;

                        _mainDataTable.Rows.Add(newRow);
                    }
                }
            }
        }

        private void EnsureColumnsExist(IEnumerable<string> columns)
        {
            foreach (var col in columns)
                if (!_mainDataTable.Columns.Contains(col))
                    _mainDataTable.Columns.Add(col);
        }

        private void AutoProcessData()
        {
            // Regex for date extraction from Batch (e.g. "Gamma143 - Shipment1 - 20260303")
            var dateRegex = new Regex(@"(\d{8})");

            foreach (DataRow row in _mainDataTable.Rows)
            {
                string dateVal = row["Date"]?.ToString();
                string batchVal = row["Batch"]?.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(dateVal))
                {
                    if (TryParseDate(dateVal, out var parsed))
                        row["Date"] = parsed.ToString("yyyy-MM-dd");
                }
                else if (!string.IsNullOrWhiteSpace(batchVal))
                {
                    var match = dateRegex.Match(batchVal.Trim());
                    if (match.Success && DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd", null, DateTimeStyles.None, out var d))
                        row["Date"] = d.ToString("yyyy-MM-dd");
                }

                string nameVal = row["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(nameVal))
                {
                    row["Name"] = _nameMatcher.Resolve(nameVal);
                }
            }
        }

        private bool TryParseDate(string text, out DateTime date)
        {
            date = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(text)) return false;

            if (DateTime.TryParse(text, out date))
                return true;

            var formats = new[]
            {
                "yyyy-MM-dd", "yyyyMMdd", "dd/MM/yyyy", "MM/dd/yyyy", "dd-MM-yyyy", "MM-dd-yyyy",
                "dd MMM yyyy", "MMM dd yyyy", "yyyy/MM/dd", "yyyy.MM.dd"
            };

            if (DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;

            // If the raw value is just 8 digits, interpret as yyyyMMdd.
            var digitsOnly = Regex.Replace(text, "[^0-9]", "");
            if (digitsOnly.Length == 8 && DateTime.TryParseExact(digitsOnly, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;

            return false;
        }

        private bool TryParseDateFromBatch(string rawBatch, out DateTime date)
        {
            date = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(rawBatch)) return false;

            var match = Regex.Match(rawBatch, @"(\d{8})");
            if (match.Success && DateTime.TryParseExact(match.Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;

            return false;
        }

        private List<string> SplitCsvLine(string line)
        {
            return Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)").ToList();
        }

        private void RefreshMapping()
        {
            // Manual mapping UI has been removed per user request.
            // Columns are now automatically mapped if headers match EXACTLY (case-insensitive).
        }

        private string FuzzyMatch(string fieldLabel, List<string> excelColumns)
        {
            var normalizedField = Normalize(fieldLabel);
            return excelColumns.FirstOrDefault(c => Normalize(c) == normalizedField) 
                ?? excelColumns.FirstOrDefault(c => Normalize(c).Contains(normalizedField));
        }

        private string Normalize(string text) => Regex.Replace(text.ToLower(), @"[^a-z0-9]", "");

        private string MapHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header)) return header;
            string normH = Normalize(header);
            var coreCols = new[] { "Date", "ManifestId", "ObjectId", "Name", "Pages", "Articles", "Characters", "Batch", "Priority", "Quality", "ShipmentFlag", "Batch2" };
            
            var match = coreCols.FirstOrDefault(c => Normalize(c) == normH);
            if (match != null) return match;

            if (normH == "id" || normH == "taskid" || normH == "task" || normH == "object") return "ObjectId";
            if (normH == "manifest") return "ManifestId";
            if (normH == "user" || normH == "username" || normH == "employee" || normH == "agent") return "Name";
            if (normH == "page" || normH == "pagecount") return "Pages";
            if (normH == "article" || normH == "articlecount") return "Articles";
            if (normH == "character" || normH == "chars" || normH == "charcount") return "Characters";
            if (normH == "shipment") return "ShipmentFlag";
            
            return header;
        }

        private void UpdateGridStats()
        {
            GridStatsText.Text = $"Total Rows: {_mainDataTable.Rows.Count} | Visible Rows: {PreviewGrid.Items.Count}";
        }

        private void GridSearch_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();
        private void GridFilter_Changed(object sender, EventArgs e) => ApplyFilters();

        private void ApplyFilters()
        {
            if (_mainDataTable == null || _mainDataTable.DefaultView == null) return;

            var filters = new List<string>();
            
            // Keyword search across all columns
            string search = GridSearchBox.Text.Trim().Replace("'", "''");
            if (!string.IsNullOrEmpty(search))
            {
                var searchParts = new List<string>();
                foreach (DataColumn col in _mainDataTable.Columns)
                    searchParts.Add($"{col.ColumnName} LIKE '%{search}%'");
                filters.Add("(" + string.Join(" OR ", searchParts) + ")");
            }

            // User filter
            if (FilterUserCombo.SelectedItem is ComboBoxItem ui && ui.Tag != null)
                filters.Add($"Name = '{ui.Content.ToString().Replace("'", "''")}'");

            // Date filter
            if (FilterDatePicker.SelectedDate.HasValue)
                filters.Add($"Date LIKE '%{FilterDatePicker.SelectedDate.Value:yyyy-MM-dd}%'");

            try
            {
                _mainDataTable.DefaultView.RowFilter = string.Join(" AND ", filters);
                UpdateGridStats();
            }
            catch { }
        }

        private void ClearGridFilters_Click(object sender, RoutedEventArgs e)
        {
            GridSearchBox.Text = "";
            FilterUserCombo.SelectedIndex = 0;
            FilterDatePicker.SelectedDate = null;
            ApplyFilters();
        }

        private void PasteData_Click(object sender, RoutedEventArgs e)
        {
            string clipboard = Clipboard.GetText();
            if (string.IsNullOrEmpty(clipboard)) return;

            string[] lines = clipboard.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            // Detect if first line is headers by checking for common keywords
            string[] firstLineParts = lines[0].Split('\t').Select(p => p.Trim()).ToArray();
            bool hasHeaders = firstLineParts.Any(p => {
                string norm = Normalize(p);
                return norm == "date" || norm == "objectid" || norm == "name" || norm == "pages" || norm == "characters" || norm == "manifest" || norm == "id";
            });

            List<string> mappings = new();
            int startRow = 0;

            if (hasHeaders)
            {
                mappings = firstLineParts.Select(h => MapHeader(h)).ToList();
                startRow = 1;
                EnsureColumnsExist(mappings);
            }
            else
            {
                // Fallback: use existing column order if no headers detected
                foreach (DataColumn col in _mainDataTable.Columns)
                    mappings.Add(col.ColumnName);
            }

            for (int i = startRow; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('\t');
                if (parts.All(string.IsNullOrWhiteSpace)) continue;
                
                var row = _mainDataTable.NewRow();
                for (int j = 0; j < parts.Length && j < mappings.Count; j++)
                {
                    string targetCol = mappings[j];
                    if (_mainDataTable.Columns.Contains(targetCol))
                        row[targetCol] = parts[j].Trim();
                }
                _mainDataTable.Rows.Add(row);
            }
            AutoProcessData();
            UpdateGridStats();
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedRows = PreviewGrid.SelectedItems.Cast<DataRowView>().ToList();
            foreach (var row in selectedRows) row.Delete();
            UpdateGridStats();
        }

        private void ClearGrid_Click(object sender, RoutedEventArgs e)
        {
            _mainDataTable.Rows.Clear();
            UpdateGridStats();
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (_mainDataTable.Rows.Count == 0)
            {
                MessageBox.Show("No data to import.");
                return;
            }

            if (ProjectCombo.SelectedItem is not ComboBoxItem pItem)
            {
                MessageBox.Show("Please select a project.");
                return;
            }

            int projectId = (int)pItem.Tag;
            var project = _projects.FirstOrDefault(p => p.Id == projectId);

            try
            {
                ImportBtn.IsEnabled = false;
                ImportBtn.Content = "Importing...";

                int imported = 0;
                int skipped = 0;
                var duplicateReasons = new List<string>();
                var seenObjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using (var conn = DatabaseService.GetConnection())
                {
                    var rowsToImport = _mainDataTable.DefaultView.Cast<DataRowView>().ToList();

                    foreach (var rv in rowsToImport)
                    {
                        var row = rv.Row;
                        try
                        {
                            string objectId = GetMapped(row, "ObjectId");
                            string name = GetMapped(row, "Name");
                            string dateStr = GetMapped(row, "Date");
                            string batchVal = GetMapped(row, "Batch");

                            if (string.IsNullOrWhiteSpace(objectId)) { skipped++; continue; }
                            if (seenObjectIds.Contains(objectId))
                            {
                                duplicateReasons.Add($"{objectId}: skipped duplicate row in import file.");
                                skipped++;
                                continue;
                            }
                            seenObjectIds.Add(objectId);

                            var existing = conn.QueryFirstOrDefault<WorkReport>(
                                "SELECT * FROM WorkReports WHERE ProjectId=@P AND ObjectId=@O",
                                new { P = projectId, O = objectId });

                            if (existing != null)
                            {
                                string existingOwner = existing.UserId > 0
                                    ? conn.ExecuteScalar<string>("SELECT FullName FROM Users WHERE Id=@Id", new { Id = existing.UserId })
                                    : null;

                                string reason;
                                if (existing.UserId == SessionManager.CurrentUser.Id)
                                    reason = "already reported by you";
                                else if (existing.UserId == 0)
                                    reason = "already imported by admin";
                                else
                                    reason = $"already reported by {existingOwner ?? "another user"}";

                                duplicateReasons.Add($"{objectId}: skipped because {reason}.");
                                skipped++;
                                continue;
                            }

                            // Resolve User
                            User user = null;
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                user = _allUsers.FirstOrDefault(u => u.FullName.Equals(name, StringComparison.OrdinalIgnoreCase) || u.Username.Equals(name, StringComparison.OrdinalIgnoreCase));
                                if (user == null)
                                    user = _allUsers.FirstOrDefault(u => u.FullName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
                            }
                            user ??= SessionManager.CurrentUser;

                            DateTime subDate = DateTime.Today;
                            if (!TryParseDate(dateStr, out var parsedDate) && TryParseDateFromBatch(batchVal, out parsedDate))
                                subDate = parsedDate;
                            else if (parsedDate != DateTime.MinValue)
                                subDate = parsedDate;

                            int reportId = Convert.ToInt32(conn.ExecuteScalar<long>(@"
                                INSERT INTO WorkReports (ProjectId, UserId, ObjectId, SubmissionDate, BillingAmount, Status)
                                VALUES (@P, @U, @O, @D, 0, 'Submitted') RETURNING Id",
                                new { P = projectId, U = user.Id, O = objectId, D = subDate }));

                            var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var field in _currentFields)
                            {
                                string val = GetMapped(row, field.FieldLabel);
                                if (!string.IsNullOrEmpty(val))
                                {
                                    conn.Execute("INSERT INTO WorkReportItems (WorkReportId, FieldId, Value) VALUES (@R, @F, @V)",
                                        new { R = reportId, F = field.Id, V = val });
                                    fieldValues[field.FieldLabel] = val;
                                }
                            }

                            if (!string.IsNullOrEmpty(project?.BillingFormula))
                            {
                                try
                                {
                                    string formula = project.BillingFormula;
                                    foreach (var f in _currentFields.Where(x => x.FieldType == "Number"))
                                    {
                                        string v = fieldValues.ContainsKey(f.FieldLabel) ? fieldValues[f.FieldLabel] : "0";
                                        formula = formula.Replace(f.FieldLabel, v);
                                    }
                                    var dt = new DataTable();
                                    double billing = Convert.ToDouble(dt.Compute(formula, ""));
                                    conn.Execute("UPDATE WorkReports SET BillingAmount=@B WHERE Id=@Id", new { B = billing, Id = reportId });
                                }
                                catch { }
                            }
                            imported++;
                        }
                        catch { skipped++; }
                    }
                }

                var message = $"Successfully imported {imported} reports. {skipped} skipped.";
                if (duplicateReasons.Any())
                {
                    message += "\n\nDuplicate details:\n" + string.Join("\n", duplicateReasons.Take(20));
                    if (duplicateReasons.Count > 20)
                        message += $"\n...and {duplicateReasons.Count - 20} more duplicates.";
                }

                MessageBox.Show(message, "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            finally
            {
                ImportBtn.IsEnabled = true;
                ImportBtn.Content = "Import Reports";
            }
        }

        private string GetMapped(DataRow row, string field)
        {
            // Exact match
            foreach (DataColumn col in row.Table.Columns)
            {
                if (col.ColumnName.Equals(field, StringComparison.OrdinalIgnoreCase))
                    return row[col]?.ToString();
            }
            
            // Fuzzy normalized match
            string normField = Normalize(field);
            foreach (DataColumn col in row.Table.Columns)
            {
                if (Normalize(col.ColumnName) == normField)
                    return row[col]?.ToString();
            }

            return null;
        }
    }
}