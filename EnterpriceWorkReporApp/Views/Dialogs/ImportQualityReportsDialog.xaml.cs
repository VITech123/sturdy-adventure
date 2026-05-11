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
    public partial class ImportQualityReportsDialog : Window
    {
        private DataTable _currentData;
        private List<Project> _projects = new();
        private Dictionary<string, string> _mapping = new();
        private List<User> _allUsers = new();
        private FuzzyNameMatcher _nameMatcher = new();

        public ImportQualityReportsDialog()
        {
            InitializeComponent();
            LoadProjects();
            LoadUsers();
            InitializeFuzzyMatcher();
            OverrideDatePicker.SelectedDate = DateTime.Today;
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
            if (_projects.Any()) ProjectCombo.SelectedIndex = 0;
        }

        private void LoadUsers()
        {
            using (var conn = DatabaseService.GetConnection())
                _allUsers = conn.Query<User>("SELECT * FROM Users WHERE IsActive=1").ToList();
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

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel & CSV Files|*.xlsx;*.xls;*.csv|All Files|*.*",
                Multiselect = false
            };
            if (dlg.ShowDialog() == true)
            {
                FilePathBox.Text = dlg.FileName;
                LoadFile(dlg.FileName);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                    });
                    if (ds.Tables.Count > 0)
                    {
                        _currentData = ds.Tables[0];
                        PreviewGrid.ItemsSource = _currentData.DefaultView;
                        RefreshMapping();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading file: " + ex.Message);
            }
        }

        private void Type_Changed(object sender, RoutedEventArgs e) => RefreshMapping();

        private void RefreshMapping()
        {
            if (_currentData == null) return;
            MappingPanel.Children.Clear();
            _mapping.Clear();

            var targetFields = TypeSummaryRadio.IsChecked == true
                ? new[] { "Name", "Accuracy", "ErrorRate", "QualityScore", "ReworkCount" }
                : new[] { "Name", "ObjectId", "Process", "Articles", "KeyedCharacters", "ActualCharacters", "DefectCharacters", "Quality" };

            var fileCols = _currentData.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

            foreach (var field in targetFields)
            {
                var panel = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                panel.Children.Add(new TextBlock { Text = field, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
                
                var combo = new ComboBox { Margin = new Thickness(10, 0, 0, 0), Style = (Style)FindResource("ModernComboBox") };
                combo.Items.Add(new ComboBoxItem { Content = "[Not Mapped]", Tag = "" });
                
                string bestMatch = fileCols.FirstOrDefault(c => c.Equals(field, StringComparison.OrdinalIgnoreCase));
                if (bestMatch == null && field == "Name") bestMatch = fileCols.FirstOrDefault(c => c.Contains("Assigned") || c.Contains("User") || c.Contains("Employee"));
                if (bestMatch == null && field == "QualityScore") bestMatch = fileCols.FirstOrDefault(c => c.Contains("Quality") || c.Contains("Score"));

                foreach (var col in fileCols)
                    combo.Items.Add(new ComboBoxItem { Content = col, Tag = col, IsSelected = col == bestMatch });
                
                if (bestMatch != null) _mapping[field] = bestMatch;

                combo.SelectionChanged += (s, a) => {
                    if (combo.SelectedItem is ComboBoxItem item && item.Tag is string val)
                        if (string.IsNullOrEmpty(val)) _mapping.Remove(field); else _mapping[field] = val;
                };

                Grid.SetColumn(combo, 1);
                panel.Children.Add(combo);
                MappingPanel.Children.Add(panel);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (_currentData == null) return;
            if (ProjectCombo.SelectedItem is not ComboBoxItem pItem) { MessageBox.Show("Select a project."); return; }
            int projectId = (int)pItem.Tag;
            DateTime reportDate = OverrideDatePicker.SelectedDate ?? DateTime.Today;

            try
            {
                ImportBtn.IsEnabled = false;
                int imported = 0;

                using (var conn = DatabaseService.GetConnection())
                {
                    bool isDetailed = TypeDetailedRadio.IsChecked == true;

                    if (isDetailed)
                    {
                        // Group by user to create summaries
                        var rows = _currentData.Rows.Cast<DataRow>();
                        var nameCol = _mapping.ContainsKey("Name") ? _mapping["Name"] : null;
                        if (nameCol == null) { MessageBox.Show("Name column must be mapped."); return; }

                        var groups = rows.GroupBy(r => _nameMatcher.Resolve(r[nameCol]?.ToString() ?? "Unknown"));

                        foreach (var group in groups)
                        {
                            string empName = group.Key;
                            var user = _allUsers.FirstOrDefault(u => u.FullName.Equals(empName, StringComparison.OrdinalIgnoreCase));
                            if (user == null) continue;

                            // Calculate summary from detailed rows
                            double totalActual = group.Sum(r => ParseDouble(GetVal(r, "ActualCharacters")));
                            double totalDefect = group.Sum(r => ParseDouble(GetVal(r, "DefectCharacters")));
                            double avgQuality = group.Average(r => ParseDouble(GetVal(r, "Quality")));
                            int reworkCount = group.Sum(r => ParseDouble(GetVal(r, "DefectCharacters")) > 0 ? 1 : 0);

                            double accuracy = totalActual > 0 ? ((totalActual - totalDefect) / totalActual) * 100 : avgQuality;

                            // Insert Summary
                            int reportId = Convert.ToInt32(conn.ExecuteScalar<long>(@"
                                INSERT INTO QualityReports (UserId, ProjectId, ReportDate, Accuracy, ErrorRate, ReworkCount, QualityScore, IsDetailed)
                                VALUES (@UserId, @ProjectId, @Date, @Acc, @Err, @Rework, @Score, 1) RETURNING Id",
                                new { UserId = user.Id, ProjectId = projectId, Date = reportDate, Acc = accuracy, Err = 100 - accuracy, Rework = reworkCount, Score = accuracy }));

                            // Insert Details
                            foreach (var row in group)
                            {
                                conn.Execute(@"
                                    INSERT INTO QualityReportItems (ReportId, ObjectId, AssignedName, Process, Articles, KeyedCharacters, ActualCharacters, DefectCharacters, Quality)
                                    VALUES (@Rid, @Oid, @Name, @Proc, @Art, @Keyed, @Actual, @Defect, @Qual)",
                                    new {
                                        Rid = reportId,
                                        Oid = GetVal(row, "ObjectId"),
                                        Name = empName,
                                        Proc = GetVal(row, "Process"),
                                        Art = ParseInt(GetVal(row, "Articles")),
                                        Keyed = ParseInt(GetVal(row, "KeyedCharacters")),
                                        Actual = ParseInt(GetVal(row, "ActualCharacters")),
                                        Defect = ParseInt(GetVal(row, "DefectCharacters")),
                                        Qual = ParseDouble(GetVal(row, "Quality"))
                                    });
                            }
                            imported++;
                        }
                    }
                    else
                    {
                        // Summary Import
                        foreach (DataRow row in _currentData.Rows)
                        {
                            string empName = _nameMatcher.Resolve(GetVal(row, "Name"));
                            var user = _allUsers.FirstOrDefault(u => u.FullName.Equals(empName, StringComparison.OrdinalIgnoreCase));
                            if (user == null) continue;

                            conn.Execute(@"
                                INSERT INTO QualityReports (UserId, ProjectId, ReportDate, Accuracy, ErrorRate, ReworkCount, QualityScore, IsDetailed)
                                VALUES (@UserId, @ProjectId, @Date, @Acc, @Err, @Rework, @Score, 0)",
                                new {
                                    UserId = user.Id,
                                    ProjectId = projectId,
                                    Date = reportDate,
                                    Acc = ParseDouble(GetVal(row, "Accuracy")),
                                    Err = ParseDouble(GetVal(row, "ErrorRate")),
                                    Rework = ParseInt(GetVal(row, "ReworkCount")),
                                    Score = ParseDouble(GetVal(row, "QualityScore"))
                                });
                            imported++;
                        }
                    }
                }
                MessageBox.Show($"Imported {imported} quality records.");
                DialogResult = true;
                Close();
            }
            catch (Exception ex) { MessageBox.Show("Import failed: " + ex.Message); }
            finally { ImportBtn.IsEnabled = true; }
        }

        private string GetVal(DataRow row, string field) => _mapping.TryGetValue(field, out var col) ? row[col]?.ToString() : "";
        private double ParseDouble(string s) => double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        private int ParseInt(string s)
        {
            if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dbl))
                return (int)Math.Round(dbl);
            return 0;
        }
    }
}
