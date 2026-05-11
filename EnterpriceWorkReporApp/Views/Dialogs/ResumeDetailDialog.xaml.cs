using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ResumeDetailDialog : Window
    {
        private Resume _resume;
        private string _selectedImagePath;

        public ResumeDetailDialog(Resume resume = null)
        {
            InitializeComponent();
            _resume = resume ?? new Resume();

            if (_resume.Id > 0)
            {
                // Edit mode
                NameBox.Text = _resume.CandidateName;
                SetStatus(_resume.Status);

                if (!string.IsNullOrEmpty(_resume.ImagePath) && File.Exists(_resume.ImagePath))
                {
                    _selectedImagePath = _resume.ImagePath;
                    ImagePathText.Text = Path.GetFileName(_selectedImagePath);
                    LoadImage(_selectedImagePath);
                }
            }
            else
            {
                StatusCombo.SelectedIndex = 0; // Default Pending
            }
        }

        private void SetStatus(string status)
        {
            foreach (System.Windows.Controls.ComboBoxItem item in StatusCombo.Items)
            {
                if (item.Content.ToString() == status)
                {
                    StatusCombo.SelectedItem = item;
                    break;
                }
            }
        }

        private void LoadImage(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                string ext = Path.GetExtension(path).ToLower();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp" && ext != ".tiff")
                {
                    ResumeImage.Source = null;
                    return;
                }

                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                ResumeImage.Source = bmp;
            }
            catch { ResumeImage.Source = null; }
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "All Supported Files|*.jpg;*.jpeg;*.png;*.pdf;*.doc;*.docx;*.txt|Image Files|*.jpg;*.jpeg;*.png|Document Files|*.pdf;*.doc;*.docx;*.txt|All Files (*.*)|*.*",
                Title = "Select Resume File"
            };

            if (dlg.ShowDialog() == true)
            {
                _selectedImagePath = dlg.FileName;
                ImagePathText.Text = Path.GetFileName(_selectedImagePath);
                LoadImage(_selectedImagePath);
                
                if (string.IsNullOrWhiteSpace(NameBox.Text))
                {
                    NameBox.Text = Path.GetFileNameWithoutExtension(_selectedImagePath).Replace(".", " ").Replace("_", " ").Replace("-", " ");
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedImagePath))
            {
                MessageBox.Show("A file must be attached.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Candidate name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedFolderName = "Uncategorized";
            using (var conn = DatabaseService.GetConnection())
            {
                int folderId = _resume.FolderId ?? 0;
                if (folderId > 0)
                    selectedFolderName = conn.ExecuteScalar<string>("SELECT Name FROM ResumeFolders WHERE Id = @Id", new { Id = folderId }) ?? "Uncategorized";
                else
                {
                    // Check if 'Pending Resumes' exists, else use 'Uncategorized'
                    var existingFolder = conn.QueryFirstOrDefault<ResumeFolder>("SELECT * FROM ResumeFolders WHERE Name = 'Pending Resumes' OR Name = 'Uncategorized' LIMIT 1");
                    if (existingFolder != null)
                    {
                        folderId = existingFolder.Id;
                        selectedFolderName = existingFolder.Name;
                    }
                    else
                    {
                        folderId = conn.ExecuteScalar<int>("INSERT INTO ResumeFolders (Name) VALUES ('Uncategorized') RETURNING Id");
                        selectedFolderName = "Uncategorized";
                    }
                }
                _resume.FolderId = folderId;
            }

            string safeFolderName = string.Join("_", selectedFolderName.Split(Path.GetInvalidFileNameChars()));
            string resumesDir = Path.Combine(DatabaseService.AppDataFolder, "resumes", safeFolderName);
            
            if (!Directory.Exists(resumesDir))
                Directory.CreateDirectory(resumesDir);

            string finalImagePath = _selectedImagePath;
            if (!finalImagePath.StartsWith(resumesDir, StringComparison.OrdinalIgnoreCase))
            {
                string ext = Path.GetExtension(_selectedImagePath);
                string newFileName = $"{Guid.NewGuid()}{ext}";
                finalImagePath = Path.Combine(resumesDir, newFileName);
                File.Copy(_selectedImagePath, finalImagePath, true);
            }

            _resume.CandidateName = NameBox.Text.Trim();
            _resume.ImagePath = finalImagePath;
            _resume.ThumbnailPath = null; // No OCR, just store file path
            _resume.Status = ((System.Windows.Controls.ComboBoxItem)StatusCombo.SelectedItem)?.Content.ToString() ?? "Pending";
            
            // Set unused fields to empty
            _resume.Email = _resume.Email ?? "";
            _resume.ContactNumber = _resume.ContactNumber ?? "";
            _resume.Experience = _resume.Experience ?? "";
            _resume.ExtractedText = _resume.ExtractedText ?? "";

            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    if (_resume.Id == 0)
                    {
                        string sql = @"
                            INSERT INTO Resumes (FolderId, CandidateName, ContactNumber, Email, Experience, ImagePath, ThumbnailPath, Status, ExtractedText, UploadDate)
                            VALUES (@FolderId, @CandidateName, @ContactNumber, @Email, @Experience, @ImagePath, @ThumbnailPath, @Status, @ExtractedText, @UploadDate)";
                        _resume.UploadDate = DateTime.Now;
                        conn.Execute(sql, _resume);
                    }
                    else
                    {
                        string sql = @"
                            UPDATE Resumes 
                            SET FolderId=@FolderId, CandidateName=@CandidateName, ContactNumber=@ContactNumber, Email=@Email, 
                                Experience=@Experience, ImagePath=@ImagePath, ThumbnailPath=@ThumbnailPath, Status=@Status, ExtractedText=@ExtractedText
                            WHERE Id=@Id";
                        conn.Execute(sql, _resume);
                    }
                }
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
