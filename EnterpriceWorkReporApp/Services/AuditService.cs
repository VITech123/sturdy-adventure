using System;
using Dapper;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Services
{
    public static class AuditService
    {
        public static void Log(string action, string details = "")
        {
            try
            {
                var user = SessionManager.CurrentUser;
                using (var conn = DatabaseService.GetConnection())
                {
                    conn.Execute(
                        "INSERT INTO AuditLogs (UserId, Username, Action, Details, Timestamp) VALUES (@UserId, @Username, @Action, @Details, @Ts)",
                        new
                        {
                            UserId = user?.Id ?? 0,
                            Username = user?.Username ?? "System",
                            Action = action,
                            Details = details,
                            Ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                }
            }
            catch
            {
                // Silently fail - logging must never crash the app
            }
        }
    }
}
