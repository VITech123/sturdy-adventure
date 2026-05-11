using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport
{
    public partial class App : Application
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data", "error.log");
        private static readonly string CrashLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data", "crash.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            // Set up global exception handlers to prevent unexpected crashes
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            
            // Handle TaskScheduler unobserved exceptions
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // Log startup
            LogMessage($"Application starting - v1.0.0 (OS: {Environment.OSVersion})");

            try
            {
                base.OnStartup(e);

                if (e.Args.Length > 0 && e.Args[0] == "--test")
                {
                    TestSuite.RunAll();
                    return;
                }

                if (e.Args.Length > 0 && e.Args[0] == "--testdata")
                {
                    DatabaseService.InitializeDatabase();
                    TestDataGenerator.GenerateAllTestData();
                    MessageBox.Show("Test data generation complete! You can now login with:\n\nAdmin: admin/admin123\nUsers: user1-user15/password123", "Test Data Generated", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Initialize database with multi-user support
                DatabaseService.InitializeDatabase();
                LogMessage("Database initialized successfully");

                // Start cloud sync automatically if configured (non-blocking)
                _ = Task.Run(() => StartCloudSyncAsync());
            }
            catch (Exception ex)
            {
                LogException(ex, "Startup");
                MessageBox.Show(
                    $"Failed to start application:\n\n{ex.Message}\n\nPlease check the error log for details.",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private async Task StartCloudSyncAsync()
        {
            try
            {
                // Small delay to let the app settle
                await Task.Delay(2000);

                using (var conn = DatabaseService.GetConnection())
                {
                    var settings = conn.QueryFirstOrDefault<CompanySettings>("SELECT * FROM CompanySettings WHERE Id = 1");
                    if (settings != null && settings.CloudSyncEnabled && !string.IsNullOrEmpty(settings.CloudDbHost))
                    {
                        var cloudService = new CloudSyncService();
                        string pwd = settings.CloudDbPassword;
                        cloudService.ConfigureCloud(
                            settings.CloudDbHost,
                            settings.CloudDbPort ?? "5432",
                            settings.CloudDbName,
                            settings.CloudDbUsername,
                            pwd
                        );
                        
                        var result = await cloudService.SyncToCloudAsync();
                        LogMessage($"Cloud sync result: {result.Message}");
                        
                        // Update last sync status in DB
                        try
                        {
                            conn.Execute(@"
                                UPDATE CompanySettings 
                                SET LastCloudSyncAt = @Now, LastCloudSyncStatus = @Status 
                                WHERE Id = 1",
                                new { Now = DateTime.Now, Status = result.Message });
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex, "CloudSyncStartup");
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var errorMsg = $"An unexpected error occurred:\n\n{e.Exception.Message}";
            LogException(e.Exception, "Dispatcher");
            
            // Show user-friendly message instead of technical details
            MessageBox.Show(
                errorMsg + "\n\nThe application will try to continue running.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            e.Handled = true; // Prevent app from crashing
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException(ex, "AppDomain");
                
                // Log critical errors to crash.log as well
                WriteCrashLog(ex);
                
                if (e.IsTerminating)
                {
                    MessageBox.Show(
                        $"A critical error occurred and the application must close.\n\n{ex.Message}\n\nError has been logged.",
                        "Critical Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
        
        private void OnUnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            LogException(e.Exception, "TaskScheduler");
            e.SetObserved(); // Prevent app crash from unobserved task exceptions
        }

        private void LogException(Exception ex, string source)
        {
            try
            {
                var logDir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
                File.AppendAllText(LogPath, logMessage);
            }
            catch
            {
                // Ignore logging errors - never crash during error handling
            }
        }
        
        private void WriteCrashLog(Exception ex)
        {
            try
            {
                var logDir = Path.GetDirectoryName(CrashLogPath);
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRASH: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n";
                File.AppendAllText(CrashLogPath, logMessage);
            }
            catch { }
        }

        private void LogMessage(string message)
        {
            try
            {
                var logDir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                    
                var logText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}\n";
                File.AppendAllText(LogPath, logText);
            }
            catch { }
        }
    }
}
