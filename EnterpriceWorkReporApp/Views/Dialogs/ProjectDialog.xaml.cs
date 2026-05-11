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
using Microsoft.Win32;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ProjectDialog : Window
    {
        private readonly Project _project;
        private readonly bool _isEdit;
        private List<ProjectField> _fields = new();
        private List<int> _deletedFieldIds = new();
        private int _tempIdCounter = -1;

        public ProjectDialog(Project existing = null)
        {
            InitializeComponent();
            _project = existing ?? new Project();
            _isEdit = existing != null;

            if (_isEdit)
            {
                TitleText.Text = "Edit Project";
                NameBox.Text = _project.Name;
                ProjectCodeBox.Text = _project.ProjectCode ?? "";
                DescriptionBox.Text = _project.Description;
                FormulaBox.Text = _project.BillingFormula;
                MasterFilePathBox.Text = _project.MasterFilePath ?? "";
                MasterFilePasswordBox.Password = SecretService.DecryptSecret(_project.MasterFilePassword) ?? "";
                QualityBasePathBox.Text = _project.QualityBasePath ?? "";
                IsActiveCheck.IsChecked = _project.IsActive;
            }
            
            LoadFields();
        }

        private void LoadFields()
        {
            if (_isEdit)
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    _fields = conn.Query<ProjectField>(
                        "SELECT * FROM ProjectFields WHERE ProjectId=@Id ORDER BY SortOrder",
                        new { Id = _project.Id }).AsList();
                }
            }
            RefreshFieldsGrid();
        }

        private void RefreshFieldsGrid()
        {
            FieldsGrid.ItemsSource = null;
            FieldsGrid.ItemsSource = _fields.OrderBy(f => f.SortOrder).ToList();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            PreviewPanel.Children.Clear();

            if (_fields.Count == 0)
            {
                PreviewPanel.Children.Add(new TextBlock 
                { 
                    Text = "No fields defined yet. Add some fields to see the preview.", 
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)),
                    FontStyle = FontStyles.Italic
                });
                return;
            }

            foreach (var field in _fields.OrderBy(f => f.SortOrder))
            {
                var label = new TextBlock
                {
                    Text = $"{field.FieldLabel.ToUpper()}{(field.IsRequired ? " *" : "")}",
                    Style = (Style)FindResource("FieldLabel"),
                    Margin = new Thickness(0, 12, 0, 6)
                };
                var input = new TextBox 
                { 
                    Style = (Style)FindResource("ModernTextBox"),
                    ToolTip = field.Instructions
                };

                PreviewPanel.Children.Add(label);
                PreviewPanel.Children.Add(input);
                
                if (!string.IsNullOrWhiteSpace(field.Instructions))
                {
                    PreviewPanel.Children.Add(new TextBlock
                    {
                        Text = field.Instructions,
                        FontSize = 11,
                        Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                }
            }
        }

        private void AddField_Click(object sender, RoutedEventArgs e)
        {
            string label = NewFieldLabelBox.Text.Trim();
            string internalName = NewInternalNameBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(label)) return;

            // Core fields to prevent duplication
            string[] coreFields = { "object id", "objectid", "date", "project", "billing", "status", "admin note", "attachment" };
            if (coreFields.Contains(label.ToLower()) || coreFields.Contains(internalName.ToLower()))
            {
                MessageBox.Show($"'{label}' is a core field and cannot be added as a custom field.", "Invalid Field Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int maxSort = _fields.Any() ? _fields.Max(f => f.SortOrder) : 0;
            
            var newField = new ProjectField
            {
                Id = _tempIdCounter--,
                ProjectId = _project.Id,
                FieldLabel = NewFieldLabelBox.Text.Trim(),
                InternalFieldName = NewInternalNameBox.Text.Trim(),
                FieldType = (NewFieldTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Text",
                IsRequired = NewFieldRequired.IsChecked == true,
                IncludeInBilling = NewFieldBilling.IsChecked == true,
                Instructions = NewInstructionsBox.Text.Trim(),
                SortOrder = maxSort + 1
            };
            
            _fields.Add(newField);

            NewFieldLabelBox.Clear();
            NewInternalNameBox.Clear();
            NewInstructionsBox.Clear();
            NewFieldRequired.IsChecked = false;
            NewFieldBilling.IsChecked = false;
            
            RefreshFieldsGrid();
        }

        private void DeleteField_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var fieldToRemove = _fields.FirstOrDefault(f => f.Id == id);
                if (fieldToRemove != null)
                {
                    _fields.Remove(fieldToRemove);
                    // Keep track of real DB fields that were deleted
                    if (id > 0)
                    {
                        _deletedFieldIds.Add(id);
                    }
                    RefreshFieldsGrid();
                }
            }
        }

        private void TestFormula_Click(object sender, RoutedEventArgs e)
        {
            string formula = FormulaBox.Text.Trim();
            if (string.IsNullOrEmpty(formula))
            {
                FormulaResultText.Text = "⚠ Enter a formula above first.";
                FormulaResultText.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                return;
            }

            string testFormula = formula;
            string valuesText = TestFormulaValuesBox.Text.Trim();
            if (!string.IsNullOrEmpty(valuesText))
            {
                foreach (var pair in valuesText.Split(','))
                {
                    var parts = pair.Split('=');
                    if (parts.Length == 2)
                    {
                        string varName = parts[0].Trim();
                        string varValue = parts[1].Trim();
                        testFormula = testFormula.Replace(varName, varValue);
                    }
                }
            }

            try
            {
                var dt = new DataTable();
                var result = dt.Compute(testFormula, "");
                double value = Convert.ToDouble(result);
                FormulaResultText.Text = $"✅ Result: ₹{value:F2}  (formula: {testFormula})";
                FormulaResultText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            }
            catch (Exception ex)
            {
                FormulaResultText.Text = $"❌ Formula error: {ex.Message}. Hint: enter sample values like 'CharacterCount=5000, Rate=2'";
                FormulaResultText.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            }
        }

        private void BrowseMasterFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Master File(s)",
                Filter = "Excel & CSV Files|*.xlsx;*.xls;*.csv|All Files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
                MasterFilePathBox.Text = string.Join(";", dlg.FileNames);
        }

        private FuzzyNameMatcher GetFuzzyMatcher()
        {
            var matcher = new FuzzyNameMatcher();
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var settings = conn.QueryFirstOrDefault<CompanySettings>(
                        "SELECT MasterNamesPath FROM CompanySettings WHERE Id = 1");
                    if (settings != null && !string.IsNullOrWhiteSpace(settings.MasterNamesPath))
                        matcher.LoadMasterNames(settings.MasterNamesPath);
                }
            }
            catch { }
            return matcher;
        }

        private void SyncMasterFile_Click(object sender, RoutedEventArgs e)
        {
            string pathsRaw = MasterFilePathBox.Text.Trim();
            if (string.IsNullOrEmpty(pathsRaw))
            {
                MessageBox.Show("Please set Master File Path(s) first.", "No Path Set",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var paths = pathsRaw.Split(';').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            var existingPaths = paths.Where(File.Exists).ToList();
            if (!existingPaths.Any())
            {
                MessageBox.Show("None of the specified files were found.", "File Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var masterService = new MasterDataService();
                var nameMatcher = GetFuzzyMatcher();
                string password = MasterFilePasswordBox.Password;

                var result = MessageBox.Show(
                    $"Found {existingPaths.Count} file(s). This will import data into the Analytics database.\n\nDo you want to:\n• Click Yes to Replace Existing records with new data\n• Click No to Add New records only",
                    "Sync Master File",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                bool replace = result == MessageBoxResult.Yes;
                int totalCount = 0;

                foreach (var path in existingPaths)
                {
                    totalCount += masterService.SyncMasterFile(_project.Id, path, replace, 
                        string.IsNullOrEmpty(password) ? null : password, nameMatcher);
                }

                DatabaseService.UpdateProjectSyncStatus(_project.Id, DateTime.Now, $"Manual sync imported {totalCount} records");

                string matchInfo = nameMatcher.IsLoaded 
                    ? $"\n\nFuzzy name matching: {nameMatcher.MasterNameCount} canonical names loaded." 
                    : "\n\n⚠ No MasterNames file configured.";
                MessageBox.Show($"✅ Successfully imported {totalCount} records from {existingPaths.Count} file(s)!{matchInfo}\n\nYou can now view the data in Analytics.",
                    "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Sync failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseQualityPath_Click(object sender, RoutedEventArgs e)
        {
            // Use COM-based folder picker (works in WPF without WinForms reference)
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Quality Report Base Folder (select any file in the folder)",
                CheckFileExists = false,
                FileName = "Select Folder",
                Filter = "Folders|*.none",
                ValidateNames = false
            };
            
            if (!string.IsNullOrEmpty(QualityBasePathBox.Text) && Directory.Exists(QualityBasePathBox.Text))
                dialog.InitialDirectory = QualityBasePathBox.Text;
            
            if (dialog.ShowDialog() == true)
                QualityBasePathBox.Text = Path.GetDirectoryName(dialog.FileName);
        }

        private void ImportQualityReports_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ImportQualityReportsDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                QualityImportStatusText.Text = "✅ Quality reports imported successfully.";
                QualityImportStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            }
        }

        /// <summary>
        /// Import a single quality summary CSV/XLSX file.
        /// Columns expected: manifest_id, Assigned Name, vendor_name, process, object_id, 
        ///                   articles, keyed_characters, actual_characters, defect_characters, quality
        /// </summary>
        private (int imported, int skipped) ImportSingleQualityFile(string filePath, DateTime reportDate, FuzzyNameMatcher nameMatcher)
        {
            List<Dictionary<string, string>> rows;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".csv")
                rows = ReadCsvRows(filePath);
            else
                rows = ReadExcelRows(filePath);

            int imported = 0, skipped = 0;

            using (var conn = DatabaseService.GetConnection())
            {
                foreach (var row in rows)
                {
                    try
                    {
                        // Get assigned name
                        string empName = GetCsvValue(row, "Assigned Name", "assigned name", "Assigned_Name");
                        if (string.IsNullOrWhiteSpace(empName))
                        {
                            skipped++;
                            continue;
                        }

                        // Fuzzy match the name
                        if (nameMatcher != null && nameMatcher.IsLoaded)
                            empName = nameMatcher.Resolve(empName);

                        // Find user in database
                        var user = conn.QueryFirstOrDefault<User>(
                            "SELECT Id FROM Users WHERE FullName ILIKE @N LIMIT 1",
                            new { N = $"%{empName.Trim()}%" });

                        int? userId = user?.Id;
                        if (userId == null)
                        {
                            // Try to find by fuzzy-matched name exact
                            user = conn.QueryFirstOrDefault<User>(
                                "SELECT Id FROM Users WHERE LOWER(FullName) = LOWER(@N) LIMIT 1",
                                new { N = empName.Trim() });
                            userId = user?.Id;
                        }

                        if (userId == null) { skipped++; continue; }

                        // Parse numeric fields
                        int keyedChars = ParseInt(GetCsvValue(row, "charactercount", "character_count", "keyed_characters", "Keyed_Characters", "KeyedCharacters", "total_keyedcharacters", "Total_KeyedCharacters"));
                        int actualChars = ParseInt(GetCsvValue(row, "charactercount", "character_count", "actual_characters", "Actual_Characters", "ActualCharacters", "total_actualcharacters", "Total_ActualCharacters"));
                        int defectChars = ParseInt(GetCsvValue(row, "defect_characters", "Defect_Characters", "DefectCharacters", "total_defectcharacters", "Total_DefectCharacters"));
                        double quality = ParseDouble(GetCsvValue(row, "quality", "Quality", "quality%", "Quality%").Replace("%", ""));

                        // Calculate accuracy and error rate
                        double accuracy = actualChars > 0 ? ((double)(actualChars - defectChars) / actualChars) * 100.0 : quality;
                        double errorRate = actualChars > 0 ? ((double)defectChars / actualChars) * 100.0 : 0;
                        if (quality > 0 && accuracy == 0) accuracy = quality;
                        double qualityScore = quality > 0 ? quality : accuracy;

                        // Build remarks from object info
                        string objectId = GetCsvValue(row, "object_id", "Object_Id", "ObjectId");
                        string manifestId = GetCsvValue(row, "manifest_id", "Manifest_Id", "ManifestId");
                        int articles = ParseInt(GetCsvValue(row, "articles", "Articles"));
                        int pages = ParseInt(GetCsvValue(row, "pages", "total_pages", "Total_Pages", "Pages"));

                        var remarkParts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(objectId)) remarkParts.Add($"Object: {objectId}");
                        if (!string.IsNullOrWhiteSpace(manifestId)) remarkParts.Add($"Manifest: {manifestId}");
                        if (articles > 0) remarkParts.Add($"Articles: {articles}");
                        if (pages > 0) remarkParts.Add($"Pages: {pages}");
                        string remarks = string.Join(", ", remarkParts);

                        int reworkCount = defectChars > 0 ? 1 : 0;

                        conn.Execute(@"
                            INSERT INTO QualityReports
                                (UserId, ReportDate, Accuracy, ErrorRate, ReworkCount, QualityScore, Remarks)
                            VALUES
                                (@UserId, @ReportDate, @Accuracy, @ErrorRate, @ReworkCount, @QualityScore, @Remarks)",
                            new
                            {
                                UserId = userId,
                                ReportDate = reportDate, // Pass DateTime object directly
                                Accuracy = Math.Round(accuracy, 2),
                                ErrorRate = Math.Round(errorRate, 2),
                                ReworkCount = reworkCount,
                                QualityScore = Math.Round(qualityScore, 2),
                                Remarks = remarks
                            });
                        imported++;
                    }
                    catch
                    {
                        skipped++;
                    }
                }
            }

            AuditService.Log("Quality Import", $"Imported {imported} records from {Path.GetFileName(filePath)} (date: {reportDate:yyyy-MM-dd})");
            return (imported, skipped);
        }

        private List<Dictionary<string, string>> ReadCsvRows(string path)
        {
            var rows = new List<Dictionary<string, string>>();
            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.UTF8); }
            catch { lines = File.ReadAllLines(path); }

            if (lines.Length < 2) return rows;

            var headers = SplitCsvLine(lines[0]).Select(h => h.Trim('"', ' ')).ToList();

            for (int i = 1; i < lines.Length; i++)
            {
                var vals = SplitCsvLine(lines[i]).Select(v => v.Trim('"', ' ')).ToList();
                if (vals.All(string.IsNullOrWhiteSpace)) continue;
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < headers.Count && c < vals.Count; c++)
                    dict[headers[c]] = vals[c];
                rows.Add(dict);
            }
            return rows;
        }

        private List<Dictionary<string, string>> ReadExcelRows(string filePath)
        {
            var rows = new List<Dictionary<string, string>>();
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                });
                if (ds.Tables.Count == 0) return rows;
                var table = ds.Tables[0];
                var cols = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                foreach (DataRow row in table.Rows)
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var col in cols)
                        dict[col] = row[col]?.ToString() ?? "";
                    if (dict.Values.All(string.IsNullOrWhiteSpace)) continue;
                    rows.Add(dict);
                }
            }
            return rows;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var vals = new List<string>();
            var cur = new StringBuilder();
            bool inQ = false;
            foreach (char c in line)
            {
                if (c == '"') { inQ = !inQ; cur.Append(c); }
                else if (c == ',' && !inQ) { vals.Add(cur.ToString()); cur.Clear(); }
                else { cur.Append(c); }
            }
            vals.Add(cur.ToString());
            return vals;
        }

        private static string GetCsvValue(Dictionary<string, string> row, params string[] keys)
        {
            foreach (var k in keys)
                if (row.TryGetValue(k, out var val) && !string.IsNullOrWhiteSpace(val))
                    return val;
            return "";
        }

        private static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;
        private static double ParseDouble(string s) => double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                ErrorText.Text = "Project name is required.";
                return;
            }

            using (var conn = DatabaseService.GetConnection())
            {
                int projectId = _project.Id;

                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        if (_isEdit)
                        {
                            conn.Execute(@"UPDATE Projects 
                                           SET Name=@N, ProjectCode=@C, Description=@D, BillingFormula=@F,
                                               MasterFilePath=@M, MasterFilePassword=@P, QualityBasePath=@Q, IsActive=@A 
                                           WHERE Id=@Id",
                                new
                                {
                                    N  = NameBox.Text.Trim(),
                                    C  = ProjectCodeBox.Text.Trim(),
                                    D  = DescriptionBox.Text.Trim(),
                                    F  = FormulaBox.Text.Trim(),
                                    M  = MasterFilePathBox.Text.Trim(),
                                    P  = SecretService.EnsureEncrypted(MasterFilePasswordBox.Password),
                                    Q  = QualityBasePathBox.Text.Trim(),
                                    A  = IsActiveCheck.IsChecked == true ? 1 : 0,
                                    Id = projectId
                                }, trans);
                            AuditService.Log("Project Updated", $"Project ID {projectId}: {NameBox.Text}");
                        }
                        else
                        {
                            projectId = Convert.ToInt32(conn.ExecuteScalar<long>(@"INSERT INTO Projects (Name, ProjectCode, Description, BillingFormula, MasterFilePath, MasterFilePassword, QualityBasePath, IsActive) 
                                           VALUES (@N, @C, @D, @F, @M, @P, @Q, @A) RETURNING Id",
                                new
                                {
                                    N = NameBox.Text.Trim(),
                                    C = ProjectCodeBox.Text.Trim(),
                                    D = DescriptionBox.Text.Trim(),
                                    F = FormulaBox.Text.Trim(),
                                    M = MasterFilePathBox.Text.Trim(),
                                    P = SecretService.EnsureEncrypted(MasterFilePasswordBox.Password),
                                    Q = QualityBasePathBox.Text.Trim(),
                                    A = 1
                                }, trans));
                            
                            AuditService.Log("Project Created", $"Project: {NameBox.Text}");
                        }

                        if (_deletedFieldIds.Any())
                        {
                            conn.Execute("DELETE FROM ProjectFields WHERE Id IN @Ids", new { Ids = _deletedFieldIds }, trans);
                        }

                        foreach (var field in _fields)
                        {
                            if (field.Id < 0) // New temp field
                            {
                                conn.Execute(@"INSERT INTO ProjectFields 
                                               (ProjectId, FieldLabel, InternalFieldName, FieldType, IsRequired, IncludeInBilling, Instructions, SortOrder)
                                               VALUES (@ProjectId, @Label, @InternalName, @Type, @Req, @Billing, @Instructions, @Sort)",
                                    new
                                    {
                                        ProjectId    = projectId,
                                        Label        = field.FieldLabel,
                                        InternalName = field.InternalFieldName,
                                        Type         = field.FieldType,
                                        Req          = field.IsRequired ? 1 : 0,
                                        Billing      = field.IncludeInBilling ? 1 : 0,
                                        Instructions = field.Instructions,
                                        Sort         = field.SortOrder
                                    }, trans);
                            }
                        }

                        trans.Commit();
                    }
                    catch (Exception)
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
