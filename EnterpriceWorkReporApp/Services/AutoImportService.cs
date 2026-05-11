using System;
using System.IO;
using System.Threading;
using Dapper;
using EnterpriseWorkReport.Models;

namespace EnterpriseWorkReport.Services
{
    public class AutoImportService : IDisposable
    {
        private FileSystemWatcher _watcher;
        private readonly string _watchPath;
        private readonly string _projectFilter;
        private bool _isRunning;
        private Timer _debounceTimer;
        private string _pendingFile;

        public event EventHandler<string> FileImported;
        public event EventHandler<string> ImportError;

        public AutoImportService(string watchPath, string projectFilter = null)
        {
            _watchPath = watchPath;
            _projectFilter = projectFilter;
        }

        public void Start()
        {
            if (_isRunning) return;

            if (!Directory.Exists(_watchPath))
            {
                Directory.CreateDirectory(_watchPath);
            }

            _watcher = new FileSystemWatcher(_watchPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;

            _isRunning = true;
            AuditService.Log("AutoImport Started", $"Monitoring folder: {_watchPath}");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _watcher?.Dispose();
            _watcher = null;
            _isRunning = false;
            AuditService.Log("AutoImport Stopped", $"Stopped monitoring: {_watchPath}");
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var ext = Path.GetExtension(e.FullPath).ToLower();
            if (ext != ".csv" && ext != ".xlsx" && ext != ".xls") return;

            _pendingFile = e.FullPath;
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(ProcessPendingFile, null, 1000, Timeout.Infinite);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            OnFileChanged(sender, e);
        }

        private void ProcessPendingFile(object state)
        {
            if (string.IsNullOrEmpty(_pendingFile)) return;

            try
            {
                var file = _pendingFile;
                _pendingFile = null;

                if (!File.Exists(file)) return;

                using (var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { }

                Thread.Sleep(500);

                ImportMasterData(file);
            }
            catch (Exception ex)
            {
                ImportError?.Invoke(this, $"Error processing file: {ex.Message}");
            }
        }

        private void ImportMasterData(string filePath)
        {
            try
            {
                var projectName = Path.GetFileName(Path.GetDirectoryName(filePath));
                if (string.IsNullOrEmpty(projectName))
                    projectName = Path.GetFileNameWithoutExtension(filePath);

                using var conn = DatabaseService.GetConnection();

                var project = conn.QueryFirstOrDefault<Project>(
                    "SELECT * FROM Projects WHERE Name LIKE @Name OR ProjectCode LIKE @Name LIMIT 1",
                    new { Name = $"%{projectName}%" });

                if (project == null)
                {
                    project = conn.QueryFirstOrDefault<Project>("SELECT * FROM Projects WHERE IsActive=1 LIMIT 1");
                }

                if (project == null)
                {
                    ImportError?.Invoke(this, "No active project found. Please create a project first.");
                    return;
                }

                var lines = File.ReadAllLines(filePath);
                int imported = 0;
                int skipped = 0;

                var headers = lines.Length > 0 ? lines[0].Split(',') : Array.Empty<string>();

                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    try
                    {
                        var cols = lines[i].Split(',');
                        if (cols.Length < 3) continue;

                        var objectId = cols[0].Trim();
                        var manifestId = cols.Length > 1 ? cols[1].Trim() : "";
                        var status = cols.Length > 2 ? cols[2].Trim() : "Unassigned";

                        long existingCount = conn.ExecuteScalar<long>(
                            "SELECT COUNT(*) FROM MasterData WHERE ObjectId = @O",
                            new { O = objectId });

                        if (existingCount > 0)
                        {
                            conn.Execute(@"
                                UPDATE MasterData SET 
                                    Status = @Status,
                                    ManifestId = @Manifest
                                WHERE ObjectId = @ObjectId",
                                new { ObjectId = objectId, Status = status, Manifest = manifestId });
                            imported++;
                        }
                        else
                        {
                            int pages = cols.Length > 3 && int.TryParse(cols[3], out var p) ? p : 0;
                            int chars = cols.Length > 4 && int.TryParse(cols[4], out var c) ? c : 0;
                            var assignedName = cols.Length > 5 ? cols[5].Trim() : "";

                            var assignedUser = conn.QueryFirstOrDefault<User>(
                                "SELECT * FROM Users WHERE FullName LIKE @Name LIMIT 1",
                                new { Name = $"%{assignedName}%" });

                            conn.Execute(@"
                                INSERT INTO MasterData (ProjectId, ManifestId, ObjectId, Pages, CharacterCount, Status, AssignedUserId, AssignedUserName)
                                VALUES (@PId, @Manifest, @OId, @Pages, @Chars, @Status, @UId, @UName)",
                                new
                                {
                                    PId = project.Id,
                                    Manifest = manifestId,
                                    OId = objectId,
                                    Pages = pages,
                                    Chars = chars,
                                    Status = status,
                                    UId = assignedUser?.Id,
                                    UName = assignedName
                                });
                            imported++;
                        }
                    }
                    catch
                    {
                        skipped++;
                    }
                }

                var msg = $"Auto-imported {imported} records from {Path.GetFileName(filePath)}";
                if (skipped > 0) msg += $" ({skipped} skipped)";
                FileImported?.Invoke(this, msg);
                AuditService.Log("MasterData AutoImport", msg);
            }
            catch (Exception ex)
            {
                ImportError?.Invoke(this, $"Import failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _debounceTimer?.Dispose();
        }
    }
}