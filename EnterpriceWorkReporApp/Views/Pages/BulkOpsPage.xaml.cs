using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;
using Microsoft.Win32;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class BulkOpsPage : Page
    {
        private string _logOutput = "";

        public BulkOpsPage()
        {
            InitializeComponent();
            
            // Security check: Only admins can access bulk operations
            if (!SessionManager.IsAdmin)
            {
                MessageBox.Show("Access Denied. Only administrators can perform bulk operations.", 
                    "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Warning);
                NavigationService?.GoBack();
                return;
            }

            LoadProjects();
        }

        private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            string tag = (sender as Button)?.Tag?.ToString();
            string header = tag switch
            {
                "WorkReports" => GetWorkReportsTemplateHeader(),
                "Attendance"  => "Username,Date(dd/MM/yyyy),Status(Present|Absent|Half Day|Leave),Remarks",
                "Quality"     => "Username,Date(dd/MM/yyyy),Accuracy,ErrorRate,ReworkCount,QualityScore,Remarks",
                "Users"       => "Username,FullName,Password,Role(Administrator|Employee)",
                _             => "No template defined"
            };

            var dlg = new SaveFileDialog { FileName = $"{tag}_Template.csv", Filter = "CSV|*.csv|TXT|*.txt" };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, header);
                AppendLog($"[{DateTime.Now:HH:mm:ss}] Template downloaded: {dlg.FileName}");
                MessageBox.Show("Template saved.", "Template", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SmartImportWorkReports_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new ImportWorkReportsDialog { Owner = mainWindow };
            dialog.ShowDialog();
        }

        private void SmartImportQuality_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new ImportQualityReportsDialog { Owner = mainWindow };
            dialog.ShowDialog();
        }

        private void SmartImportAttendance_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new ImportAttendanceDialog { Owner = mainWindow };
            dialog.ShowDialog();
        }

        private void SmartImportUsers_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new ImportUsersDialog { Owner = mainWindow };
            dialog.ShowDialog();
        }

        private void ImportFile_Click(object sender, RoutedEventArgs e)
        {
            string tag = (sender as Button)?.Tag?.ToString();
            var dlg = new OpenFileDialog 
            { 
                Filter = "All Supported|*.csv;*.txt;*.json;*.xml;*.xlsx|CSV Files|*.csv|Text Files|*.txt|JSON Files|*.json|XML Files|*.xml;*.xlsx|All Files|*.*", 
                Title = $"Import {tag}" 
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var extension = Path.GetExtension(dlg.FileName).ToLower();
                List<string[]> rows = new List<string[]>();

                // Parse file based on extension
                if (extension == ".json")
                {
                    rows = ParseJsonFile(dlg.FileName);
                }
                else if (extension == ".xml")
                {
                    rows = ParseXmlFile(dlg.FileName);
                }
                else
                {
                    // CSV or TXT
                    rows = ParseDelimitedFile(dlg.FileName);
                }

                int imported = 0;

                using (var conn = DatabaseService.GetConnection())
                {
                    // Skip header line (index 0)
                    for (int i = 1; i < rows.Count; i++)
                    {
                        var cols = rows[i];
                        if (cols.Length == 0 || string.IsNullOrWhiteSpace(string.Join("", cols))) continue;

                        try
                        {
                            if (tag == "Attendance" && cols.Length >= 3)
                            {
                                var user = conn.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Username=@U", new { U = cols[0].Trim() });
                                if (user == null) continue;
                                if (!DateTime.TryParseExact(cols[1].Trim(), "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var date)) continue;

                                conn.Execute(@"INSERT INTO Attendance (UserId, Date, Status, Remarks) VALUES (@UserId, @Date, @Status, @Remarks) ON CONFLICT (UserId, Date) DO UPDATE SET Status = EXCLUDED.Status, Remarks = EXCLUDED.Remarks",
                                    new { UserId = user.Id, Date = date, Status = cols[2].Trim(), Remarks = cols.Length > 3 ? cols[3].Trim() : "" });
                                imported++;
                            }
                            else if (tag == "Users" && cols.Length >= 4)
                            {
                                // Hash the password if it's not already hashed
                                var password = cols[2].Trim();
                                if (!password.StartsWith("$")) // Simple check if not hashed
                                {
                                    password = AuthService.HashPassword(password);
                                }
                                conn.Execute(@"INSERT INTO Users (Username, FullName, PasswordHash, Role) VALUES (@U, @FN, @PH, @R) ON CONFLICT (Username) DO NOTHING",
                                    new { U = cols[0].Trim(), FN = cols[1].Trim(), PH = password, R = cols[3].Trim() });
                                imported++;
                            }
                            else if (tag == "Quality" && cols.Length >= 6)
                            {
                                var user = conn.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Username=@U", new { U = cols[0].Trim() });
                                if (user == null) continue;
                                if (!DateTime.TryParseExact(cols[1].Trim(), "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var date)) continue;

                                conn.Execute(@"INSERT INTO QualityReports (UserId, ReportDate, Accuracy, ErrorRate, ReworkCount, QualityScore, Remarks) VALUES (@UserId, @D, @A, @E, @R, @Q, @Rem)",
                                    new { UserId = user.Id, D = date, A = double.Parse(cols[2]), E = double.Parse(cols[3]), R = int.Parse(cols[4]), Q = double.Parse(cols[5]), Rem = cols.Length > 6 ? cols[6].Trim() : "" });
                                imported++;
                            }
                            else if (tag == "WorkReports" && cols.Length >= 4)
                            {
                                var user = conn.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Username=@U", new { U = cols[0].Trim() });
                                var project = conn.QueryFirstOrDefault<Project>("SELECT * FROM Projects WHERE Name=@N AND IsActive=1", new { N = cols[1].Trim() });
                                
                                if (user == null || project == null)
                                {
                                    AppendLog($"[{DateTime.Now:HH:mm:ss}] Row {i + 1} skipped: User or Project not found.");
                                    continue;
                                }

                                string objectId = cols[2].Trim();
                                if (!DateTime.TryParseExact(cols[3].Trim(), "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var subDate))
                                {
                                    AppendLog($"[{DateTime.Now:HH:mm:ss}] Row {i + 1} skipped: Invalid date format (use dd/MM/yyyy).");
                                    continue;
                                }

                                // Check for existing ObjectId to avoid duplicates
                                long existing = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM WorkReports WHERE ProjectId=@P AND ObjectId=@O", new { P = project.Id, O = objectId });
                                if (existing > 0)
                                {
                                    AppendLog($"[{DateTime.Now:HH:mm:ss}] Row {i + 1} skipped: Object ID '{objectId}' already exists for this project.");
                                    continue;
                                }

                                // Create Work Report
                                int reportId = Convert.ToInt32(conn.ExecuteScalar<long>(@"INSERT INTO WorkReports (ProjectId, UserId, ObjectId, SubmissionDate, BillingAmount) VALUES (@P, @U, @O, @D, 0.0) RETURNING Id",
                                    new { P = project.Id, U = user.Id, O = objectId, D = subDate }));

                                // Process Dynamic Fields
                                var fields = conn.Query<ProjectField>("SELECT * FROM ProjectFields WHERE ProjectId=@Id", new { Id = project.Id }).ToList();
                                var headers = rows[0];
                                var fieldValues = new Dictionary<string, string>();

                                for (int c = 4; c < cols.Length && c < headers.Length; c++)
                                {
                                    string header = headers[c].Trim();
                                    string val = cols[c].Trim();
                                    var field = fields.FirstOrDefault(f => f.FieldLabel.Equals(header, StringComparison.OrdinalIgnoreCase));
                                    if (field != null)
                                    {
                                        conn.Execute("INSERT INTO WorkReportItems (WorkReportId, FieldId, Value) VALUES (@R, @F, @V)",
                                            new { R = reportId, F = field.Id, V = val });
                                        fieldValues[field.FieldLabel] = val;
                                    }
                                }

                                // Calculate Billing
                                if (!string.IsNullOrWhiteSpace(project.BillingFormula))
                                {
                                    try
                                    {
                                        string formula = project.BillingFormula;
                                        foreach (var field in fields.Where(f => f.FieldType == "Number"))
                                        {
                                            string val = fieldValues.ContainsKey(field.FieldLabel) ? fieldValues[field.FieldLabel] : "0";
                                            if (double.TryParse(val, out double numVal))
                                            {
                                                formula = formula.Replace(field.FieldLabel, numVal.ToString());
                                            }
                                        }
                                        var dt = new System.Data.DataTable();
                                        var billingResult = dt.Compute(formula, "");
                                        double billing = Convert.ToDouble(billingResult);
                                        conn.Execute("UPDATE WorkReports SET BillingAmount=@B WHERE Id=@Id", new { B = billing, Id = reportId });
                                    }
                                    catch { /* Formula error, keep billing as 0 */ }
                                }

                                imported++;
                            }
                        }
                        catch (Exception rowEx)
                        {
                            AppendLog($"[{DateTime.Now:HH:mm:ss}] Row {i + 1} error: {rowEx.Message}");
                        }
                    }
                }

                AppendLog($"[{DateTime.Now:HH:mm:ss}] {tag} import complete: {imported} records imported from {Path.GetFileName(dlg.FileName)}");
                MessageBox.Show($"Import complete! {imported} records imported.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ERROR during {tag} import: {ex.Message}");
                MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<string[]> ParseDelimitedFile(string filePath)
        {
            var result = new List<string[]>();
            var lines = File.ReadAllLines(filePath);
            char delimiter = Path.GetExtension(filePath).ToLower() == ".tab" ? '\t' : ',';
            
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    result.Add(line.Split(delimiter));
                }
            }
            return result;
        }

        private List<string[]> ParseJsonFile(string filePath)
        {
            var result = new List<string[]>();
            var json = File.ReadAllText(filePath);
            
            // Simple JSON parser for array of arrays or array of objects
            try
            {
                json = json.Trim();
                if (json.StartsWith("["))
                {
                    json = json.Trim('[', ']');
                    var items = json.Split(new[] { "},{" }, StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var item in items)
                    {
                        var cleanItem = item.Trim('{', '}', '[', ']');
                        if (string.IsNullOrEmpty(cleanItem)) continue;
                        
                        var values = cleanItem.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(v => v.Split(':').LastOrDefault()?.Trim('"', ' ') ?? "")
                            .ToArray();
                        result.Add(values);
                    }
                }
            }
            catch { }
            
            return result;
        }

        private List<string[]> ParseXmlFile(string filePath)
        {
            var result = new List<string[]>();
            var doc = XDocument.Load(filePath);
            
            if (doc.Root != null)
            {
                var firstElement = doc.Root.Elements().FirstOrDefault();
                if (firstElement != null)
                {
                    // Add header row
                    var headers = firstElement.Attributes().Select(a => a.Name.LocalName).ToArray();
                    result.Add(headers);
                    
                    // Add data rows
                    foreach (var element in doc.Root.Elements())
                    {
                        var row = headers.Select(h => element.Attribute(h)?.Value ?? element.Element(h)?.Value ?? "").ToArray();
                        result.Add(row);
                    }
                }
            }
            
            return result;
        }

        private void AppendLog(string msg)
        {
            _logOutput += msg + "\n";
            ImportLogText.Text = _logOutput;
        }

        private void LoadProjects()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var projects = conn.Query<Project>("SELECT * FROM Projects WHERE IsActive = 1 ORDER BY Name").ToList();
                ProjectSelector.ItemsSource = projects;
                if (projects.Any()) ProjectSelector.SelectedIndex = 0;
            }
        }

        private void UploadMasterFile_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectSelector.SelectedItem is not Project selectedProject)
            {
                MessageBox.Show("Please select a project first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls|CSV Files|*.csv|All Files|*.*",
                Title = "Select Master File"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var masterDataService = new MasterDataService();
                selectedProject.MasterFilePassword = SecretService.DecryptSecret(selectedProject.MasterFilePassword);
                int imported = masterDataService.SyncMasterFile(selectedProject.Id, dlg.FileName, false, selectedProject.MasterFilePassword);
                DatabaseService.UpdateProjectSyncStatus(selectedProject.Id, DateTime.Now, $"Manual upload imported {imported} records");

                AppendLog($"[{DateTime.Now:HH:mm:ss}] Master file uploaded: {imported} records imported for project '{selectedProject.Name}'");
                MessageBox.Show($"Master file uploaded successfully!\n{imported} records imported.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ERROR uploading master file: {ex.Message}");
                MessageBox.Show($"Upload failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetWorkReportsTemplateHeader()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var firstProject = conn.QueryFirstOrDefault<Project>("SELECT * FROM Projects WHERE IsActive=1 ORDER BY Id LIMIT 1");
                if (firstProject != null)
                {
                    var fields = conn.Query<ProjectField>("SELECT FieldLabel FROM ProjectFields WHERE ProjectId=@Id ORDER BY SortOrder", new { Id = firstProject.Id });
                    string fieldHeaders = string.Join(",", fields.Select(f => f.FieldLabel));
                    if (!string.IsNullOrEmpty(fieldHeaders))
                        return $"Username,ProjectName,ObjectId,Date(dd/MM/yyyy),{fieldHeaders}";
                }
            }
            return "Username,ProjectName,ObjectId,Date(dd/MM/yyyy),Field1_Label,Field2_Label...";
        }
    }
}
