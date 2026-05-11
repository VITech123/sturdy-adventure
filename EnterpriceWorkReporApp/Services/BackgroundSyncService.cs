using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Services
{
    /// <summary>
    /// Service that periodically checks configured project master files and synchronizes them with the database.
    /// </summary>
    public class BackgroundSyncService : IDisposable
    {
        private static BackgroundSyncService _instance;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private readonly int _pollIntervalMinutes = 15;
        private readonly MasterDataService _masterDataService = new MasterDataService();
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly Dictionary<string, DateTime> _lastChangeTimes = new(StringComparer.OrdinalIgnoreCase);

        public event Action<string> SyncLog;
        public event Action<int> MasterDataUpdated;

        public static BackgroundSyncService Instance => _instance ??= new BackgroundSyncService();

        private BackgroundSyncService() { }

        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            _cts = new CancellationTokenSource();
            
            Task.Run(() => SyncLoop(_cts.Token), _cts.Token);
            Task.Run(() => SetupFileWatchers(), _cts.Token);
            AuditService.Log("BackgroundSync", "Service started (Polling interval: " + _pollIntervalMinutes + " mins)");
        }

        public void Stop()
        {
            if (!_isRunning) return;
            
            _cts?.Cancel();
            _isRunning = false;
            DisposeWatchers();
            AuditService.Log("BackgroundSync", "Service stopped");
        }

        private async Task SyncLoop(CancellationToken token)
        {
            // Initial delay to let the app finish loading
            await Task.Delay(TimeSpan.FromSeconds(30), token);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await PerformSync(token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    SyncLog?.Invoke($"[Error] Background sync failed: {ex.Message}");
                    AuditService.Log("BackgroundSync Error", ex.Message);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_pollIntervalMinutes), token);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task SetupFileWatchers()
        {
            DisposeWatchers();
            List<Project> activeProjects;
            using (var conn = DatabaseService.GetConnection())
            {
                activeProjects = conn.Query<Project>("SELECT * FROM Projects WHERE IsActive = 1 AND MasterFilePath IS NOT NULL AND MasterFilePath <> ''").ToList();
            }

            foreach (var project in activeProjects)
            {
                project.MasterFilePassword = SecretService.DecryptSecret(project.MasterFilePassword);
                try
                {
                    string filePath = project.MasterFilePath;
                    string directory = Path.GetDirectoryName(filePath);
                    string fileName = Path.GetFileName(filePath);

                    if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(directory))
                        continue;

                    var watcher = new FileSystemWatcher(directory, fileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                        EnableRaisingEvents = true,
                        IncludeSubdirectories = false
                    };

                    FileSystemEventHandler handler = (s, e) => OnMasterFileChanged(project.Id, filePath, project.MasterFilePassword);
                    RenamedEventHandler renameHandler = (s, e) => OnMasterFileChanged(project.Id, filePath, project.MasterFilePassword);

                    watcher.Changed += handler;
                    watcher.Created += handler;
                    watcher.Deleted += handler;
                    watcher.Renamed += renameHandler;

                    _watchers.Add(watcher);
                }
                catch { }
            }
        }

        private void DisposeWatchers()
        {
            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch { }
            }
            _watchers.Clear();
            _lastChangeTimes.Clear();
        }

        private void OnMasterFileChanged(int projectId, string filePath, string password)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            var now = DateTime.UtcNow;
            lock (_lastChangeTimes)
            {
                if (_lastChangeTimes.TryGetValue(filePath, out var last) && (now - last).TotalSeconds < 3)
                    return;
                _lastChangeTimes[filePath] = now;
            }

            Task.Run(() =>
            {
                try
                {
                    SyncLog?.Invoke($"Detected file change for project {projectId}: {filePath}");
                    int count = _masterDataService.SyncMasterFile(projectId, filePath, false, string.IsNullOrWhiteSpace(password) ? null : password);
                    if (count > 0)
                    {
                        SyncLog?.Invoke($"Auto-synced {count} records from {Path.GetFileName(filePath)}.");
                        DatabaseService.UpdateProjectSyncStatus(projectId, DateTime.Now, $"Auto sync imported {count} records");
                        MasterDataUpdated?.Invoke(projectId);
                    }
                    else
                    {
                        DatabaseService.UpdateProjectSyncStatus(projectId, DateTime.Now, "Auto sync found no changes");
                    }
                }
                catch (Exception ex)
                {
                    SyncLog?.Invoke($"Auto-sync failed for {filePath}: {ex.Message}");
                    DatabaseService.UpdateProjectSyncStatus(projectId, DateTime.Now, $"Auto sync failed: {ex.Message}");
                }
            });
        }

        public async Task PerformSync(CancellationToken token = default)
        {
            SyncLog?.Invoke("Starting background synchronization...");
            
            List<Project> activeProjects;
            using (var conn = DatabaseService.GetConnection())
            {
                activeProjects = conn.Query<Project>("SELECT * FROM Projects WHERE IsActive = 1").ToList();
            }

            int totalImported = 0;
            foreach (var project in activeProjects)
            {
                if (token.IsCancellationRequested) break;
                
                if (string.IsNullOrEmpty(project.MasterFilePath)) continue;

                project.MasterFilePassword = SecretService.DecryptSecret(project.MasterFilePassword);

                try
                {
                    SyncLog?.Invoke($"Checking {project.Name} master file: {project.MasterFilePath}");
                    
                    // Run sync in a background task
                    int count = await Task.Run(() => 
                        _masterDataService.SyncMasterFile(project.Id, project.MasterFilePath, false, project.MasterFilePassword), 
                        token);
                    
                    if (count > 0)
                    {
                        totalImported += count;
                        SyncLog?.Invoke($"✓ Successfully imported {count} new/updated records for {project.Name}");
                        AuditService.Log("BackgroundSync", $"Synced {count} records for {project.Name}");
                        DatabaseService.UpdateProjectSyncStatus(project.Id, DateTime.Now, $"Auto sync imported {count} records");
                    }
                    else
                    {
                        SyncLog?.Invoke($"✓ Checked {project.Name}: no changes detected.");
                        DatabaseService.UpdateProjectSyncStatus(project.Id, DateTime.Now, "Auto sync found no changes");
                    }
                }
                catch (Exception ex)
                {
                    SyncLog?.Invoke($"✕ Failed to sync {project.Name}: {ex.Message}");
                    DatabaseService.UpdateProjectSyncStatus(project.Id, DateTime.Now, $"Auto sync failed: {ex.Message}");
                }
            }

            SyncLog?.Invoke($"Background sync completed. Total records updated: {totalImported}");
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
