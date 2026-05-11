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
    public partial class WorkReportDialog : Window
    {
        private readonly WorkReport _report;
        private readonly bool _isEdit;
        private List<Project> _projects = new();
        private List<ProjectField> _currentFields = new();
        private readonly Dictionary<int, TextBox> _fieldInputs = new();
        private bool _anySaved = false;
        private string _attachmentPath = null;

        public WorkReportDialog(WorkReport existing = null)
        {
            InitializeComponent();
            _report = existing ?? new WorkReport { SubmissionDate = DateTime.Today };
            _isEdit = existing != null;

            if (_isEdit)
            {
                TitleText.Text = "Edit Work Report";
                _attachmentPath = _report.AttachmentPath;
                UpdateAttachmentUI();
            }
            
            ReportDate.SelectedDate = _report.SubmissionDate.Date;
            AdminNoteBox.Text = _report.AdminNote ?? "";

            if (!SessionManager.IsAdmin)
            {
                AdminNoteBox.IsReadOnly = true;
                AdminNoteBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251)); // #F9FAFB
                if (string.IsNullOrWhiteSpace(_report.AdminNote))
                    AdminNotePanel.Visibility = Visibility.Collapsed;
            }

            LoadProjects();
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

            if (_isEdit)
                foreach (ComboBoxItem item in ProjectCombo.Items)
                    if ((int)item.Tag == _report.ProjectId)
                    { ProjectCombo.SelectedItem = item; break; }
            else if (_projects.Any())
                ProjectCombo.SelectedIndex = 0;
        }

        private void ObjectIdBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DuplicateWarningText == null) return;
            
            string objectId = ObjectIdBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(objectId) || (ProjectCombo.SelectedItem is not ComboBoxItem item))
            {
                DuplicateWarningText.Visibility = Visibility.Collapsed;
                return;
            }

            int projectId = (int)item.Tag;

            // Don't warn if it's the original object ID in edit mode
            if (_isEdit && objectId == _report.ObjectId)
            {
                DuplicateWarningText.Visibility = Visibility.Collapsed;
                return;
            }

            // Check DB asynchronously or with a small delay for better perf if needed
            // For now, simple check
            using (var conn = DatabaseService.GetConnection())
            {
                var exists = conn.ExecuteScalar<bool>(
                    "SELECT EXISTS(SELECT 1 FROM WorkReports WHERE ProjectId = @P AND ObjectId = @O)",
                    new { P = projectId, O = objectId });
                
                DuplicateWarningText.Visibility = exists ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void Project_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectCombo.SelectedItem is ComboBoxItem item && item.Tag is int projectId)
                LoadDynamicFields(projectId);
        }

        private void LoadDynamicFields(int projectId)
        {
            DynamicFieldsPanel.Children.Clear();
            _fieldInputs.Clear();

            using (var conn = DatabaseService.GetConnection())
            {
                _currentFields = conn.Query<ProjectField>(
                    "SELECT * FROM ProjectFields WHERE ProjectId=@Id ORDER BY SortOrder",
                    new { Id = projectId }).AsList();
            }

            // Resolve styles safely via Application resources (avoids StaticResourceHolder crash
            // when the Window is not yet in the visual tree at constructor time)
            var fieldLabelStyle = Application.Current.TryFindResource("FieldLabel") as Style;
            var textBoxStyle    = Application.Current.TryFindResource("ModernTextBox") as Style;

            foreach (var field in _currentFields)
            {
                var label = new TextBlock
                {
                    Text   = $"{field.FieldLabel.ToUpper()}{(field.IsRequired ? " *" : "")}",
                    Style  = fieldLabelStyle,
                    Margin = new Thickness(0, 12, 0, 6)
                };
                var input = new TextBox { Style = textBoxStyle };

                // Pre-fill on edit — match by FieldId first, then fall back to FieldLabel
                if (_isEdit && _report.Items != null)
                {
                    var existing = _report.Items.FirstOrDefault(i => i.FieldId == field.Id)
                                ?? _report.Items.FirstOrDefault(i =>
                                       !string.IsNullOrEmpty(i.FieldLabel) &&
                                       i.FieldLabel.Equals(field.FieldLabel, StringComparison.OrdinalIgnoreCase));
                    if (existing != null) input.Text = existing.Value ?? "";
                }

                DynamicFieldsPanel.Children.Add(label);
                DynamicFieldsPanel.Children.Add(input);
                _fieldInputs[field.Id] = input;
            }
        }

        private void CalculateBilling_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectCombo.SelectedItem is not ComboBoxItem item) return;
            int projectId = (int)item.Tag;

            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (string.IsNullOrWhiteSpace(project?.BillingFormula))
            {
                BillingBox.Text = "₹0.00";
                return;
            }

            try
            {
                // Build variable dictionary from numeric fields
                string formula = project.BillingFormula;
                foreach (var field in _currentFields.Where(f => f.FieldType == "Number"))
                {
                    if (_fieldInputs.TryGetValue(field.Id, out var input) &&
                        double.TryParse(input.Text, out double val))
                    {
                        formula = formula.Replace(field.FieldLabel, val.ToString());
                    }
                }

                var dt = new DataTable();
                var result = dt.Compute(formula, "");
                double billing = Convert.ToDouble(result);
                BillingBox.Text = $"₹{billing:F2}";
            }
            catch
            {
                BillingBox.Text = "Formula error";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectCombo.SelectedItem is not ComboBoxItem item)
            { ErrorText.Text = "Please select a project."; return; }
            if (string.IsNullOrWhiteSpace(ObjectIdBox.Text))
            { ErrorText.Text = "Object ID is required."; return; }

            int projectId = (int)item.Tag;
            string objectId = ObjectIdBox.Text.Trim();

            // Check for duplicate Object ID (only for new reports or if Object ID changed)
            if (!_isEdit || objectId != _report.ObjectId)
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var existing = conn.QueryFirstOrDefault<WorkReport>(
                        "SELECT * FROM WorkReports WHERE ProjectId = @ProjectId AND ObjectId = @ObjectId",
                        new { ProjectId = projectId, ObjectId = objectId });

                    if (existing != null)
                    {
                        var result = MessageBox.Show(
                            $"Object ID '{objectId}' already exists for this project.\n\n" +
                            $"Submitted: {existing.SubmissionDate:dd/MM/yyyy}  |  Billing: ₹{existing.BillingAmount:F2}\n\n" +
                            "Do you want to REPLACE (overwrite) the existing entry?",
                            "Duplicate Object ID Detected",
                            MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result != MessageBoxResult.Yes)
                        {
                            ErrorText.Text = "Submission cancelled. Please use a unique Object ID.";
                            return;
                        }

                        // Delete the old record so the new save replaces it
                        using (var conn2 = DatabaseService.GetConnection())
                        {
                            conn2.Execute("DELETE FROM WorkReportItems WHERE WorkReportId=@Id", new { Id = existing.Id });
                            conn2.Execute("DELETE FROM WorkReports WHERE Id=@Id", new { Id = existing.Id });
                        }
                        AuditService.Log("Work Report Replaced", $"Object ID: {objectId} replaced by User {SessionManager.CurrentUser?.Id}");
                    }
                }
            }

            // Validate required fields
            foreach (var field in _currentFields.Where(f => f.IsRequired))
            {
                if (_fieldInputs.TryGetValue(field.Id, out var inp) && string.IsNullOrWhiteSpace(inp.Text))
                {
                    ErrorText.Text = $"'{field.FieldLabel}' is required.";
                    return;
                }
            }

            // Parse billing
            double billing = 0;
            if (BillingBox.Text.StartsWith("₹") &&
                double.TryParse(BillingBox.Text.TrimStart('₹'), out double b))
                billing = b;

            using (var conn = DatabaseService.GetConnection())
            {
                int reportId;
                string adminNote = SessionManager.IsAdmin ? AdminNoteBox.Text.Trim() : _report.AdminNote;

                if (_isEdit)
                {
                    conn.Execute(@"UPDATE WorkReports SET ProjectId=@P, ObjectId=@O, SubmissionDate=@D, BillingAmount=@B, AdminNote=@A, AttachmentPath=@Att WHERE Id=@Id",
                        new { P = projectId, O = objectId, D = ReportDate.SelectedDate ?? DateTime.Today, B = billing, A = adminNote, Att = _attachmentPath, Id = _report.Id });
                    conn.Execute("DELETE FROM WorkReportItems WHERE WorkReportId=@Id", new { Id = _report.Id });
                    reportId = _report.Id;
                }
                else
                {
                    reportId = Convert.ToInt32(conn.ExecuteScalar<long>(@"INSERT INTO WorkReports (ProjectId, UserId, ObjectId, SubmissionDate, BillingAmount, AdminNote, AttachmentPath)
                                   VALUES (@P, @U, @O, @D, @B, @A, @Att) RETURNING Id",
                        new { P = projectId, U = SessionManager.CurrentUser.Id, O = objectId, D = ReportDate.SelectedDate ?? DateTime.Today, B = billing, A = adminNote, Att = _attachmentPath }));
                }

                // Save dynamic field values
                foreach (var kvp in _fieldInputs)
                {
                    conn.Execute("INSERT INTO WorkReportItems (WorkReportId, FieldId, Value) VALUES (@R, @F, @V)",
                        new { R = reportId, F = kvp.Key, V = kvp.Value.Text });
                }
            }

            AuditService.Log(_isEdit ? "Work Report Updated" : "Work Report Submitted", $"Object ID: {objectId}");
            
            if (!_isEdit && AddAnotherCheck.IsChecked == true)
            {
                _anySaved = true;
                ObjectIdBox.Text = "";
                BillingBox.Text = "";
                _attachmentPath = null;
                UpdateAttachmentUI();
                foreach (var input in _fieldInputs.Values) input.Text = "";
                
                ErrorText.Foreground = (Application.Current.TryFindResource("SuccessBrush") as System.Windows.Media.Brush)
                                      ?? System.Windows.Media.Brushes.Green;
                ErrorText.Text = "Report saved successfully! You can add another or click Close/Cancel.";
                ObjectIdBox.Focus();
            }
            else
            {
                DialogResult = true;
                Close();
            }
        }

        private void AttachFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Attachment",
                Filter = "All Files|*.*|Documents|*.pdf;*.docx;*.xlsx|Images|*.jpg;*.png"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string folder = System.IO.Path.Combine(DatabaseService.AppDataFolder, "Attachments");
                    System.IO.Directory.CreateDirectory(folder);
                    
                    string ext = System.IO.Path.GetExtension(dlg.FileName);
                    string target = System.IO.Path.Combine(folder, $"att_{DateTime.Now.Ticks}{ext}");
                    System.IO.File.Copy(dlg.FileName, target, true);
                    
                    _attachmentPath = target;
                    UpdateAttachmentUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to copy attachment: " + ex.Message);
                }
            }
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            _attachmentPath = null;
            UpdateAttachmentUI();
        }

        private void UpdateAttachmentUI()
        {
            if (string.IsNullOrEmpty(_attachmentPath))
            {
                AttachmentNameText.Text = "No file attached";
                RemoveAttachmentBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                AttachmentNameText.Text = System.IO.Path.GetFileName(_attachmentPath);
                RemoveAttachmentBtn.Visibility = Visibility.Visible;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) 
        { 
            if (_anySaved) DialogResult = true;
            else DialogResult = false; 
            Close(); 
        }
    }
}
