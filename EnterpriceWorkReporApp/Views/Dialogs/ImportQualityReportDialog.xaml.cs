using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using ExcelDataReader;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ImportQualityReportDialog : Window
    {
        // Columns read from the file
        private List<string> _fileColumns = new List<string>();
        // All rows from the file (after header)
        private List<Dictionary<string, string>> _fileRows = new List<Dictionary<string, string>>();

        // System field -> friendly name
        private static readonly Dictionary<string, string> SystemFields = new Dictionary<string, string>
        {
            { "(ignore)",           "(Ignore this column)" },
            { "EmployeeName",       "Employee Name / Vendor Name" },
            { "ReportDate",         "Report Date" },
            { "ObjectId",           "Object ID / Job ID" },
            { "TotalPages",         "Total Pages" },
            { "KeyedCharacters",    "Keyed / Typed Characters" },
            { "ActualCharacters",   "Actual Characters" },
            { "DefectCharacters",   "Defect / Error Characters" },
            { "QualityScore",       "Quality Score (%)" },
            { "Accuracy",           "Accuracy (%)" },
            { "ErrorRate",          "Error Rate (%)" },
            { "ReworkCount",        "Rework Count" },
            { "Remarks",            "Remarks / Notes" }
        };

        // Auto-detection patterns: CSV header keyword  -> system field key
        private static readonly Dictionary<string, string> AutoDetectPatterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Vendor_Name",             "EmployeeName" },
            { "VendorName",              "EmployeeName" },
            { "Vendor Name",             "EmployeeName" },
            { "Employee",                "EmployeeName" },
            { "Employee_Name",           "EmployeeName" },
            { "EmployeeName",            "EmployeeName" },
            { "Name",                    "EmployeeName" },

            { "Object_Id",               "ObjectId" },
            { "ObjectId",                "ObjectId" },
            { "Object Id",               "ObjectId" },
            { "Job_Id",                  "ObjectId" },
            { "JobId",                   "ObjectId" },
            { "Issue_Id",                "ObjectId" },

            { "Total_Pages",             "TotalPages" },
            { "TotalPages",              "TotalPages" },
            { "Total Pages",             "TotalPages" },
            { "Pages",                   "TotalPages" },

            { "Total_KeyedCharacters",   "KeyedCharacters" },
            { "KEYED_CHARACTERS",        "KeyedCharacters" },
            { "KeyedCharacters",         "KeyedCharacters" },
            { "Keyed Characters",        "KeyedCharacters" },

            { "Total_ActualCharacters",  "ActualCharacters" },
            { "ACTUAL_CHARACTERS",       "ActualCharacters" },
            { "ActualCharacters",        "ActualCharacters" },
            { "Actual Characters",       "ActualCharacters" },

            { "Total_DefectCharacters",  "DefectCharacters" },
            { "DEFECT_CHARACTERS",       "DefectCharacters" },
            { "DefectCharacters",        "DefectCharacters" },
            { "Defect Characters",       "DefectCharacters" },
            { "Error_Characters",        "DefectCharacters" },

            { "Quality%",                "QualityScore" },
            { "QualityScore",            "QualityScore" },
            { "Quality Score",           "QualityScore" },
            { "Quality_Score",           "QualityScore" },
            { "Quality",                 "QualityScore" },

            { "Accuracy",                "Accuracy" },
            { "Accuracy%",               "Accuracy" },

            { "ErrorRate",               "ErrorRate" },
            { "Error_Rate",              "ErrorRate" },
            { "Error Rate",              "ErrorRate" },

            { "ReworkCount",             "ReworkCount" },
            { "Rework_Count",            "ReworkCount" },
            { "Rework Count",            "ReworkCount" },

            { "ReportDate",              "ReportDate" },
            { "Report_Date",             "ReportDate" },
            { "Date",                    "ReportDate" },

            { "Assigned Name",           "EmployeeName" },
            { "Assigned_Name",           "EmployeeName" },
            { "Manifest_Id",             "Remarks" },
            { "ManifestId",              "Remarks" },
            { "Articles",                "Remarks" },

            { "Remarks",                 "Remarks" },
            { "Notes",                   "Remarks" },
        };

        // The live per-column mapping: fileColumnName -> systemFieldKey
        private Dictionary<string, string> _mapping = new Dictionary<string, string>();

        // UI rows for the mapping panel
        private readonly List<(string ColName, ComboBox DropDown)> _mappingRows
            = new List<(string, ComboBox)>();

        public ImportQualityReportDialog()
        {
            InitializeComponent();
        }

        // ------------------------------------------------------------------
        // File browsing
        // ------------------------------------------------------------------
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Spreadsheet & CSV Files|*.csv;*.xlsx;*.xls|CSV Files|*.csv|Excel Files|*.xlsx;*.xls|All Files|*.*",
                Title = "Select Quality Report Files",
                Multiselect = true // Enable multiple file selection
            };

            if (dlg.ShowDialog() == true)
            {
                FilePathBox.Text = string.Join(", ", dlg.FileNames.Select(Path.GetFileName));
                LoadMultipleFiles(dlg.FileNames);
            }
        }

        private void LoadMultipleFiles(string[] paths)
        {
            _fileRows.Clear();
            _fileColumns.Clear();

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
                    MessageBox.Show($"Error reading file {Path.GetFileName(path)}:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            BuildMappingUI();
        }

        private void AppendCsv(string path)
        {
            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.UTF8); }
            catch { lines = File.ReadAllLines(path); }

            if (lines.Length == 0) return;

            var headers = SplitCsvLine(lines[0]).Select(h => h.Trim('"', ' ')).ToList();
            if (!_fileColumns.Any()) _fileColumns = headers;

            for (int i = 1; i < lines.Length; i++)
            {
                var vals = SplitCsvLine(lines[i]).Select(v => v.Trim('"', ' ')).ToList();
                if (vals.All(string.IsNullOrWhiteSpace)) continue;
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < headers.Count && c < vals.Count; c++)
                    row[headers[c]] = vals[c];
                _fileRows.Add(row);
            }
        }

        private void AppendExcel(string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                });

                if (ds.Tables.Count == 0) return;

                var table = ds.Tables[0];
                var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                if (!_fileColumns.Any()) _fileColumns = headers;

                foreach (DataRow row in table.Rows)
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var col in headers)
                        dict[col] = row[col]?.ToString() ?? "";
                    if (dict.Values.All(string.IsNullOrWhiteSpace)) continue;
                    _fileRows.Add(dict);
                }
            }
        }

        // ------------------------------------------------------------------
        // CSV loader
        // ------------------------------------------------------------------
        private void LoadCsv(string path)
        {
            // Try UTF-8 first, fall back to default
            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.UTF8); }
            catch { lines = File.ReadAllLines(path); }

            if (lines.Length == 0) throw new InvalidDataException("CSV file is empty.");

            // Header
            _fileColumns = SplitCsvLine(lines[0]).Select(h => h.Trim('"', ' ')).ToList();

            // Data rows
            _fileRows = new List<Dictionary<string, string>>();
            for (int i = 1; i < lines.Length; i++)
            {
                var vals = SplitCsvLine(lines[i]).Select(v => v.Trim('"', ' ')).ToList();
                if (vals.All(string.IsNullOrWhiteSpace)) continue;
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < _fileColumns.Count && c < vals.Count; c++)
                    row[_fileColumns[c]] = vals[c];
                _fileRows.Add(row);
            }

            PreviewLabel.Text = $"Loaded CSV: {_fileRows.Count} data rows, {_fileColumns.Count} columns.";
        }

        private static List<string> SplitCsvLine(string line)
        {
            var vals  = new List<string>();
            var cur   = new StringBuilder();
            bool inQ  = false;
            foreach (char c in line)
            {
                if (c == '"')  { inQ = !inQ; cur.Append(c); }
                else if (c == ',' && !inQ) { vals.Add(cur.ToString()); cur.Clear(); }
                else           { cur.Append(c); }
            }
            vals.Add(cur.ToString());
            return vals;
        }

        // ------------------------------------------------------------------
        // Excel loader (using ExcelDataReader)
        // ------------------------------------------------------------------
        private void LoadExcel(string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                });

                if (ds.Tables.Count == 0) throw new InvalidDataException("Excel file has no sheets.");

                var table = ds.Tables[0];
                _fileColumns = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

                _fileRows = new List<Dictionary<string, string>>();
                foreach (DataRow row in table.Rows)
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var col in _fileColumns)
                        dict[col] = row[col]?.ToString() ?? "";
                    if (dict.Values.All(string.IsNullOrWhiteSpace)) continue;
                    _fileRows.Add(dict);
                }
            }

            PreviewLabel.Text = $"Loaded Excel: {_fileRows.Count} data rows, {_fileColumns.Count} columns.";
        }

        // ------------------------------------------------------------------
        // Build the mapping UI dynamically
        // ------------------------------------------------------------------
        private void BuildMappingUI()
        {
            MappingPanel.Children.Clear();
            _mappingRows.Clear();
            _mapping.Clear();

            // Header row
            var hdr = new Grid();
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddToGrid(hdr, MakeLabel("CSV / Excel Column", bold: true), 0);
            AddToGrid(hdr, MakeLabel("Map to System Field", bold: true), 1);
            MappingPanel.Children.Add(hdr);

            // One row per column in the file
            foreach (var col in _fileColumns)
            {
                var grid = new Grid { Margin = new Thickness(0, 3, 0, 0) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Left: file column name
                AddToGrid(grid, MakeLabel(col), 0);

                // Right: ComboBox with system fields
                var combo = new ComboBox { Margin = new Thickness(8, 0, 0, 0) };
                combo.Style = TryFindResource("ModernComboBox") as Style;
                foreach (var kv in SystemFields)
                    combo.Items.Add(new ComboBoxItem { Content = kv.Value, Tag = kv.Key });

                // Auto-select
                string autoKey = AutoDetect(col);
                combo.SelectedIndex = autoKey != null
                    ? FindComboIndex(combo, autoKey)
                    : 0; // "(ignore)"

                combo.Tag = col; // remember which file column this row is for
                combo.SelectionChanged += Mapping_Changed;

                AddToGrid(grid, combo, 1);
                MappingPanel.Children.Add(grid);
                _mappingRows.Add((col, combo));

                // Store initial auto-detected mapping
                if (autoKey != null)
                    _mapping[col] = autoKey;
            }
        }

        private void Mapping_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.Tag is string colName)
            {
                string key = (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "(ignore)";
                if (key == "(ignore)")
                    _mapping.Remove(colName);
                else
                    _mapping[colName] = key;
            }
        }

        private string AutoDetect(string colName)
        {
            // Exact match first
            if (AutoDetectPatterns.TryGetValue(colName, out var v1)) return v1;
            // Partial / contains match (case-insensitive)
            foreach (var kv in AutoDetectPatterns)
                if (colName.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0
                    || kv.Key.IndexOf(colName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kv.Value;
            return null;
        }

        private static int FindComboIndex(ComboBox combo, string key)
        {
            for (int i = 0; i < combo.Items.Count; i++)
                if ((combo.Items[i] as ComboBoxItem)?.Tag?.ToString() == key)
                    return i;
            return 0;
        }

        // Helpers
        private static TextBlock MakeLabel(string text, bool bold = false) => new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Margin = new Thickness(0, 0, 4, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        private static void AddToGrid(Grid g, UIElement el, int col)
        {
            Grid.SetColumn(el, col);
            g.Children.Add(el);
        }

        // ------------------------------------------------------------------
        // Import
        // ------------------------------------------------------------------
        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (_fileRows.Count == 0)
            {
                MessageBox.Show("Please select a file first.", "Import",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_mapping.ContainsValue("EmployeeName") && !_mapping.ContainsValue("QualityScore")
                && !_mapping.ContainsValue("ActualCharacters"))
            {
                if (MessageBox.Show(
                    "No employee name or quality column is mapped.\n\nContinue anyway?",
                    "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    return;
            }

            try
            {
                // Determine report date: from DatePicker or today
                DateTime reportDate = ReportDatePicker.SelectedDate ?? DateTime.Today;

                // Load Fuzzy Matcher if path is configured
                var nameMatcher = new FuzzyNameMatcher();
                try
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        var settings = conn.QueryFirstOrDefault<CompanySettings>(
                            "SELECT MasterNamesPath FROM CompanySettings WHERE Id = 1");
                        if (settings != null && !string.IsNullOrWhiteSpace(settings.MasterNamesPath))
                            nameMatcher.LoadMasterNames(settings.MasterNamesPath);
                    }
                }
                catch { }

                int imported = 0, skipped = 0;

                using (var conn = DatabaseService.GetConnection())
                {
                    foreach (var row in _fileRows)
                    {
                        // Resolve employee
                        string empName   = GetValue(row, "EmployeeName");
                        int?   userId    = null;

                        if (!string.IsNullOrWhiteSpace(empName))
                        {
                            // Fuzzy match name first
                            if (nameMatcher.IsLoaded)
                                empName = nameMatcher.Resolve(empName);

                            var user = conn.QueryFirstOrDefault<User>(
                                "SELECT Id FROM Users WHERE FullName ILIKE @N LIMIT 1",
                                new { N = $"%{empName.Trim()}%" });
                            
                            if (user == null)
                            {
                                // Try exact match on fuzzy-resolved name
                                user = conn.QueryFirstOrDefault<User>(
                                    "SELECT Id FROM Users WHERE LOWER(FullName) = LOWER(@N) LIMIT 1",
                                    new { N = empName.Trim() });
                            }
                            userId = user?.Id;
                        }

                        if (userId == null)
                            userId = SessionManager.CurrentUser?.Id;

                        if (userId == null) { skipped++; continue; }

                        // Resolve date from file if mapped
                        string dateStr = GetValue(row, "ReportDate");
                        if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
                            reportDate = parsedDate;

                        // Numeric fields
                        int    keyedChars   = ParseInt(GetValue(row, "KeyedCharacters"));
                        int    actualChars  = ParseInt(GetValue(row, "ActualCharacters"));
                        int    defectChars  = ParseInt(GetValue(row, "DefectCharacters"));
                        int    reworkCount  = ParseInt(GetValue(row, "ReworkCount"));
                        double qualityScore = ParseDouble(GetValue(row, "QualityScore").Replace("%", ""));
                        double accuracy     = ParseDouble(GetValue(row, "Accuracy").Replace("%", ""));
                        double errorRate    = ParseDouble(GetValue(row, "ErrorRate").Replace("%", ""));

                        // Calculate accuracy / error rate if not mapped but chars are available
                        if (actualChars > 0)
                        {
                            if (accuracy == 0)
                                accuracy = ((double)(actualChars - defectChars) / actualChars) * 100.0;
                            if (errorRate == 0)
                                errorRate = ((double)defectChars / actualChars) * 100.0;
                        }
                        if (qualityScore == 0 && accuracy > 0)
                            qualityScore = accuracy;

                        // Build remarks
                        string objectId = GetValue(row, "ObjectId");
                        string remarks  = GetValue(row, "Remarks");
                        if (string.IsNullOrWhiteSpace(remarks))
                        {
                            var parts = new List<string>();
                            if (!string.IsNullOrWhiteSpace(objectId))  parts.Add($"Object: {objectId}");
                            int tp = ParseInt(GetValue(row, "TotalPages"));
                            if (tp > 0)   parts.Add($"Pages: {tp}");
                            remarks = string.Join(", ", parts);
                        }

                        conn.Execute(@"
                            INSERT INTO QualityReports
                                (UserId, ReportDate, Accuracy, ErrorRate, ReworkCount, QualityScore, Remarks)
                            VALUES
                                (@UserId, @ReportDate, @Accuracy, @ErrorRate, @ReworkCount, @QualityScore, @Remarks)",
                            new
                            {
                                UserId      = userId,
                                ReportDate  = reportDate, // Pass DateTime object directly to fix 42804 error
                                Accuracy    = accuracy,
                                ErrorRate   = errorRate,
                                ReworkCount = reworkCount == 0 && defectChars > 0 ? 1 : reworkCount,
                                QualityScore= qualityScore,
                                Remarks     = remarks
                            });
                        imported++;
                    }
                }

                string msg = $"Import complete.\n\n✅ Imported:  {imported} records";
                if (skipped > 0) msg += $"\n⚠️ Skipped:   {skipped} rows (employee not found & no current user)";
                MessageBox.Show(msg, "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = imported > 0;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during import:\n{ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private string GetValue(Dictionary<string, string> row, string systemField)
        {
            foreach (var kv in _mapping)
                if (kv.Value == systemField && row.TryGetValue(kv.Key, out var val))
                    return val ?? "";
            return "";
        }

        private static int    ParseInt(string s)    => int.TryParse(s, out var v)    ? v : 0;
        private static double ParseDouble(string s) => double.TryParse(s, System.Globalization.NumberStyles.Any,
                                                           System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
