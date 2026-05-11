using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class ProjectsPage : Page
    {
        private List<Project> _allProjects = new List<Project>();

        public ProjectsPage()
        {
            InitializeComponent();
            
            if (!SessionManager.IsAdmin)
            {
                AddProjectBtn.Visibility = Visibility.Collapsed;
                ActionsCol.Visibility = Visibility.Collapsed;
            }
            
            LoadProjects();
        }

        private void LoadProjects()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                _allProjects = conn.Query<Project>("SELECT * FROM Projects ORDER BY Name").AsList();
            }
            foreach (var project in _allProjects)
            {
                project.MasterFilePassword = SecretService.DecryptSecret(project.MasterFilePassword);
            }
            ProjectsGrid.ItemsSource = _allProjects;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox?.Text?.ToLower() ?? "";
            ProjectsGrid.ItemsSource = string.IsNullOrWhiteSpace(query)
                ? _allProjects
                : _allProjects.Where(p => (p.Name ?? "").ToLower().Contains(query)).ToList();
        }

        private void AddProject_Click(object sender, RoutedEventArgs e)
        {
            // Get the parent window to use as Owner
            var mainWindow = Window.GetWindow(this);
            var dialog = new ProjectDialog { Owner = mainWindow };
            if (dialog.ShowDialog() == true)
                LoadProjects();
        }

        private void EditProject_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var project = _allProjects.FirstOrDefault(p => p.Id == id);
                if (project != null)
                {
                    var mainWindow = Window.GetWindow(this);
                    var dialog = new ProjectDialog(project) { Owner = mainWindow };
                    if (dialog.ShowDialog() == true)
                        LoadProjects();
                }
            }
        }

        private void ManageFields_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var project = _allProjects.FirstOrDefault(p => p.Id == id);
                if (project != null)
                {
                    var mainWindow = Window.GetWindow(this);
                    var dialog = new ProjectFieldsDialog(project) { Owner = mainWindow };
                    dialog.ShowDialog();
                    LoadProjects();
                }
            }
        }

        private void ProjectsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    }
}
