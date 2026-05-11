using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class ResumesPage : Page
    {
        private List<Resume> _allResumes = new List<Resume>();
        private List<ResumeFolder> _folders = new List<ResumeFolder>();
        private ResumeFolder _selectedFolder;

        public ResumesPage()
        {
            InitializeComponent();
            FilterStatus.SelectedIndex = 0;
            LoadFolders();
            LoadResumes();
        }

        private void LoadFolders()
        {
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    _folders = conn.Query<ResumeFolder>(@"
                        SELECT f.*, (SELECT COUNT(*) FROM Resumes r WHERE r.FolderId = f.Id) as DocumentCount 
                        FROM ResumeFolders f ORDER BY f.Name").ToList();
                    
                    // Add "All Documents" virtual folder
                    _folders.Insert(0, new ResumeFolder { Id = -1, Name = "All Documents", Icon = "📂", DocumentCount = _allResumes.Count });
                }
                FoldersList.ItemsSource = _folders;
                if (_selectedFolder == null) FoldersList.SelectedIndex = 0;
                
                BuildMoveMenu();
            }
            catch (Exception ex)
            {
                NotificationService.Show("Error", $"Failed to load folders: {ex.Message}", NotificationType.Error);
            }
        }

        private void BuildMoveMenu()
        {
            MoveToFolderMenu.Items.Clear();
            foreach (var folder in _folders.Where(f => f.Id != -1))
            {
                var item = new MenuItem { Header = folder.Name, Tag = folder.Id };
                item.Click += MoveResumeToFolder_Click;
                MoveToFolderMenu.Items.Add(item);
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await SyncFolders();
            LoadResumes();
        }

        private async System.Threading.Tasks.Task SyncFolders()
        {
            try
            {
                string resumesRoot = System.IO.Path.Combine(DatabaseService.AppDataFolder, "resumes");
                if (!System.IO.Directory.Exists(resumesRoot)) System.IO.Directory.CreateDirectory(resumesRoot);

                var dirs = System.IO.Directory.GetDirectories(resumesRoot);
                if (dirs.Length == 0)
                {
                    NotificationService.Show("Sync", "No subfolders found in app_data/resumes. Please add resumes into category folders.", NotificationType.Info);
                    return;
                }

                NotificationService.Show("Sync Started", $"Syncing resumes from {dirs.Length} folders...", NotificationType.Info);

                await System.Threading.Tasks.Task.Run(() =>
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        var existingFolders = conn.Query<ResumeFolder>("SELECT * FROM ResumeFolders").ToList();
                        var existingResumes = conn.Query<string>("SELECT ImagePath FROM Resumes").ToHashSet(StringComparer.OrdinalIgnoreCase);

                        foreach (var dir in dirs)
                        {
                            string folderName = System.IO.Path.GetFileName(dir);
                            var folder = existingFolders.FirstOrDefault(f => f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));
                            if (folder == null)
                            {
                                int folderId = conn.ExecuteScalar<int>("INSERT INTO ResumeFolders (Name) VALUES (@N) RETURNING Id", new { N = folderName });
                                folder = new ResumeFolder { Id = folderId, Name = folderName };
                                existingFolders.Add(folder);
                            }

                            foreach (var file in System.IO.Directory.GetFiles(dir))
                            {
                                string ext = System.IO.Path.GetExtension(file).ToLower();
                                if (ext == ".pdf" || ext == ".docx" || ext == ".txt" || ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                                {
                                    if (!existingResumes.Contains(file))
                                    {
                                        try
                                        {
                                            var resume = new Resume
                                            {
                                                FolderId = folder.Id,
                                                CandidateName = Path.GetFileNameWithoutExtension(file).Replace("_", " ").Replace("-", " "),
                                                ContactNumber = string.Empty,
                                                Email = string.Empty,
                                                Experience = string.Empty,
                                                ImagePath = file,
                                                ThumbnailPath = null, // No OCR
                                                Status = "Pending",
                                                ExtractedText = string.Empty,
                                                UploadDate = DateTime.Now
                                            };

                                            string sql = @"
                                                INSERT INTO Resumes (FolderId, CandidateName, ContactNumber, Email, Experience, ImagePath, ThumbnailPath, Status, ExtractedText, UploadDate)
                                                VALUES (@FolderId, @CandidateName, @ContactNumber, @Email, @Experience, @ImagePath, @ThumbnailPath, @Status, @ExtractedText, @UploadDate)";
                                            conn.Execute(sql, resume);
                                        }
                                        catch { /* Skip individual file errors */ }
                                    }
                                }
                            }
                        }
                    }
                });

                NotificationService.Show("Sync Complete", "Resumes synchronized with local folders.", NotificationType.Success);
            }
            catch (Exception ex)
            {
                NotificationService.Show("Sync Error", ex.Message, NotificationType.Error);
            }
        }

        private void LoadResumes()
        {
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    _allResumes = conn.Query<Resume>("SELECT * FROM Resumes ORDER BY UploadDate DESC").ToList();
                }
                LoadFolders();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                NotificationService.Show("Database Error", $"Failed to load resumes: {ex.Message}", NotificationType.Error);
            }
        }

        private void ApplyFilters(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_allResumes == null) return;
            var filtered = _allResumes.AsEnumerable();

            if (FilterStatus != null && FilterStatus.SelectedIndex > 0)
            {
                string status = ((ComboBoxItem)FilterStatus.SelectedItem).Content.ToString();
                filtered = filtered.Where(r => r.Status == status);
            }

            if (SearchBox != null)
            {
                string q = SearchBox.Text.ToLower();
                if (!string.IsNullOrWhiteSpace(q))
                {
                    filtered = filtered.Where(r => 
                        (r.CandidateName?.ToLower().Contains(q) == true) ||
                        (r.Email?.ToLower().Contains(q) == true) ||
                        (r.ContactNumber?.ToLower().Contains(q) == true));
                }
            }

            if (_selectedFolder != null && _selectedFolder.Id != -1)
            {
                filtered = filtered.Where(r => r.FolderId == _selectedFolder.Id);
            }

            var results = filtered.ToList();
            if (ResumesGrid != null) ResumesGrid.ItemsSource = results;
            if (ResumesGallery != null) ResumesGallery.ItemsSource = results;
        }

        private void FoldersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedFolder = FoldersList.SelectedItem as ResumeFolder;
            if (FilterStatus != null) FilterStatus.SelectedIndex = 0; // Reset to All Status when folder changes
            ApplyFilters();
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var window = new Window
            {
                Title = "New Folder",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new StackPanel { Margin = new Thickness(16) };
            stackPanel.Children.Add(new TextBlock { Text = "Enter Folder Name:", Margin = new Thickness(0, 0, 0, 8) });
            var textBox = new TextBox { Width = 250 };
            stackPanel.Children.Add(textBox);
            var button = new Button { Content = "Create", Width = 80, Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            button.Click += (s, ev) => window.DialogResult = true;
            stackPanel.Children.Add(button);
            window.Content = stackPanel;

            if (window.ShowDialog() == true)
            {
                string folderName = textBox.Text;
                if (string.IsNullOrWhiteSpace(folderName)) return;

                try
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        await conn.ExecuteAsync("INSERT INTO ResumeFolders (Name) VALUES (@N) ON CONFLICT DO NOTHING", new { N = folderName });
                    }
                    LoadFolders();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create folder: {ex.Message}");
                }
            }
        }

        private async void MoveResumeToFolder_Click(object sender, RoutedEventArgs e)
        {
            if (ResumesGrid.SelectedItem is Resume selectedResume && (sender as MenuItem)?.Tag is int folderId)
            {
                try
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        // Get new folder name for physical path
                        string folderName = conn.ExecuteScalar<string>("SELECT Name FROM ResumeFolders WHERE Id = @Id", new { Id = folderId });
                        if (string.IsNullOrEmpty(folderName)) return;

                        string safeFolderName = string.Join("_", folderName.Split(System.IO.Path.GetInvalidFileNameChars()));
                        string newDir = System.IO.Path.Combine(DatabaseService.AppDataFolder, "resumes", safeFolderName);
                        
                        if (!System.IO.Directory.Exists(newDir))
                            System.IO.Directory.CreateDirectory(newDir);

                        string oldPath = selectedResume.ImagePath;
                        string fileName = System.IO.Path.GetFileName(oldPath);
                        string newPath = System.IO.Path.Combine(newDir, fileName);

                        // Physically move file if it exists and path is different
                        if (System.IO.File.Exists(oldPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                        {
                            System.IO.File.Move(oldPath, newPath);
                            selectedResume.ImagePath = newPath;
                        }

                        await conn.ExecuteAsync("UPDATE Resumes SET FolderId = @FId, ImagePath = @Path WHERE Id = @Id", 
                            new { FId = folderId, Path = selectedResume.ImagePath, Id = selectedResume.Id });
                    }
                    NotificationService.Show("Moved", "Resume moved physically and in database.", NotificationType.Success);
                    LoadResumes();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to move resume: {ex.Message}");
                }
            }
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void Search_Changed(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ResumesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResumesGrid.SelectedItem is Resume selectedResume)
            {
                OpenResumeDialog(selectedResume);
            }
        }

        private void ResumesGallery_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResumesGallery.SelectedItem is Resume selectedResume)
            {
                OpenResumeDialog(selectedResume);
            }
        }

        private void OpenResumeFile_Click(object sender, RoutedEventArgs e)
        {
            string path = null;
            if ((sender as Button)?.Tag is string s) path = s;
            else if ((sender as Button)?.Tag is Resume r) path = r.ImagePath;

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { System.Diagnostics.Process.Start(path); }
                catch (Exception ex) { MessageBox.Show($"Failed to open file: {ex.Message}"); }
            }
        }

        private void ViewResume_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Resume selectedResume)
            {
                OpenResumeFile_Click(sender, e); // Just open the file
            }
        }

        private void AddResume_Click(object sender, RoutedEventArgs e)
        {
            OpenResumeDialog(new Resume());
        }

        private void OpenResumeDialog(Resume resume)
        {
            var dialog = new ResumeDetailDialog(resume);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                LoadResumes();
            }
        }

        private void DeleteResume_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int resumeId)
            {
                if (MessageBox.Show("Are you sure you want to delete this resume?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = DatabaseService.GetConnection())
                        {
                            // Optional: delete the physical file as well
                            var resume = conn.QueryFirstOrDefault<Resume>("SELECT * FROM Resumes WHERE Id = @Id", new { Id = resumeId });
                            if (resume != null && !string.IsNullOrEmpty(resume.ImagePath) && System.IO.File.Exists(resume.ImagePath))
                            {
                                try { System.IO.File.Delete(resume.ImagePath); } catch { }
                            }
                            
                            conn.Execute("DELETE FROM Resumes WHERE Id = @Id", new { Id = resumeId });
                        }
                        NotificationService.Show("Success", "Resume deleted successfully", NotificationType.Success);
                        LoadResumes();
                    }
                    catch (Exception ex)
                    {
                        NotificationService.Show("Error", $"Failed to delete resume: {ex.Message}", NotificationType.Error);
                    }
                }
            }
        }
    }
}
