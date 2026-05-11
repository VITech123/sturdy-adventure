using System;
using System.Data;
using System.IO;
using System.Linq;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Services
{
    public static class TestSuite
    {
        public static void RunAll()
        {
            Console.WriteLine("=== Starting Automated Test Suite ===");
            try
            {
                // 1. Database Initialization Test
                string testDbDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data");
                string testDbPath = Path.Combine(testDbDir, "database.db");
                
                DatabaseService.InitializeDatabase();
                if (!File.Exists(testDbPath)) throw new Exception("Database file not created.");
                Console.WriteLine("[PASS] Database Initialization");

                // 2. Auth Test (Admin Seeding)
                using (var conn = DatabaseService.GetConnection())
                {
                    var admin = conn.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Username='admin'");
                    if (admin == null || admin.Role != "Administrator") throw new Exception("Admin not seeded.");
                    SessionManager.Login(admin);
                }
                Console.WriteLine("[PASS] Auth & Admin Seeding");

                // 3. Project & Field Creation Test
                int projectId;
                using (var conn = DatabaseService.GetConnection())
                {
                    conn.Execute("DELETE FROM Projects WHERE Name='TestProject'");
                    conn.Execute("INSERT INTO Projects (Name, Description, BillingFormula, IsActive) VALUES (@N, @D, @F, 1)",
                        new { N = "TestProject", D = "Test Description", F = "(WordCount / 1000) * Rate" });
                    projectId = conn.QuerySingle<int>("SELECT Id FROM Projects WHERE Name='TestProject'");

                    conn.Execute("INSERT INTO ProjectFields (ProjectId, FieldLabel, FieldType, IsRequired, IncludeInBilling, SortOrder) VALUES (@P, 'WordCount', 'Number', 1, 1, 1)", new { P = projectId });
                    conn.Execute("INSERT INTO ProjectFields (ProjectId, FieldLabel, FieldType, IsRequired, IncludeInBilling, SortOrder) VALUES (@P, 'Rate', 'Number', 1, 1, 2)", new { P = projectId });
                }
                Console.WriteLine("[PASS] Project & Custom Fields Creation");

                // 4. Billing Formula Engine Demo Test
                // Emulate dynamic calculation from WorkReportDialog
                string formula = "(WordCount / 1000) * Rate";
                double expected = (5000.0 / 1000.0) * 15.0; // 75.0
                string evalFormula = formula.Replace("WordCount", "5000").Replace("Rate", "15");
                var dt = new DataTable();
                double calculated = Convert.ToDouble(dt.Compute(evalFormula, ""));
                if (Math.Abs(calculated - expected) > 0.01) throw new Exception($"Billing calculation failed. Expected {expected}, got {calculated}");
                Console.WriteLine("[PASS] Billing Calculation Engine");

                // 5. Work Report Submission Test
                using (var conn = DatabaseService.GetConnection())
                {
                    int reportId = conn.QuerySingle<int>("INSERT INTO WorkReports (ProjectId, UserId, ObjectId, SubmissionDate, BillingAmount) VALUES (@P, @U, 'TEST-OBJ-1', @D, @B) RETURNING Id",
                        new { P = projectId, U = SessionManager.CurrentUser.Id, D = DateTime.Now, B = calculated });
                    
                    conn.Execute("INSERT INTO WorkReportItems (WorkReportId, FieldId, Value) VALUES (@R, (SELECT Id FROM ProjectFields WHERE FieldLabel='WordCount' AND ProjectId=@P), '5000')", new { R = reportId, P = projectId });
                    conn.Execute("INSERT INTO WorkReportItems (WorkReportId, FieldId, Value) VALUES (@R, (SELECT Id FROM ProjectFields WHERE FieldLabel='Rate' AND ProjectId=@P), '15')", new { R = reportId, P = projectId });
                    
                    var report = conn.QueryFirstOrDefault<WorkReport>("SELECT * FROM WorkReports WHERE ObjectId='TEST-OBJ-1'");
                    if (report == null) throw new Exception("Work report insertion failed.");
                }
                Console.WriteLine("[PASS] Work Report Submission");

                // 6. Attendance & Leaf Test
                using (var conn = DatabaseService.GetConnection())
                {
                    try
                    {
                        conn.Execute("INSERT INTO Attendance (UserId, Date, Status, Remarks) VALUES (@U, @D, 'Present', 'Test') ON CONFLICT (UserId, Date) DO UPDATE SET Status = EXCLUDED.Status, Remarks = EXCLUDED.Remarks",
                            new { U = SessionManager.CurrentUser.Id, D = DateTime.Today });
                    }
                    catch
                    {
                        conn.Execute("INSERT INTO Attendance (UserId, Date, Status, Remarks) VALUES (@U, @D, 'Present', 'Test')",
                            new { U = SessionManager.CurrentUser.Id, D = DateTime.Today });
                    }
                    var att = conn.QueryFirstOrDefault<Attendance>("SELECT * FROM Attendance WHERE UserId=@U", new { U = SessionManager.CurrentUser.Id });
                    if (att == null) throw new Exception("Attendance marking failed.");
                }
                Console.WriteLine("[PASS] Attendance Operations");

                // 7. Quality Report Test
                using (var conn = DatabaseService.GetConnection())
                {
                    conn.Execute("INSERT INTO QualityReports (UserId, ReportDate, Accuracy, ErrorRate, ReworkCount, QualityScore, Remarks) VALUES (@U, @D, 95.5, 4.5, 1, 98.0, 'Test')",
                        new { U = SessionManager.CurrentUser.Id, D = DateTime.Today });
                    var qr = conn.QueryFirstOrDefault<QualityReport>("SELECT * FROM QualityReports WHERE Remarks='Test'");
                    if (qr == null) throw new Exception("Quality report insertion failed.");
                }
                Console.WriteLine("[PASS] Quality Report Operations");

                // 8. Resume Management Test
                using (var conn = DatabaseService.GetConnection())
                {
                    conn.Execute("DELETE FROM Resumes WHERE CandidateName='Test Candidate'");
                    int resumeId = conn.QuerySingle<int>(@"
                        INSERT INTO Resumes (CandidateName, Email, ContactNumber, Experience, Status, UploadDate, ImagePath) 
                        VALUES (@N, @E, @P, @X, 'Pending', @D, 'test.jpg') RETURNING Id",
                        new { N = "Test Candidate", E = "test@example.com", P = "1234567890", X = "5 Years", D = DateTime.Now });
                    
                    var res = conn.QueryFirstOrDefault<Resume>("SELECT * FROM Resumes WHERE Id=@Id", new { Id = resumeId });
                    if (res == null || res.CandidateName != "Test Candidate") throw new Exception("Resume insertion failed.");
                    
                    conn.Execute("UPDATE Resumes SET Status='Ongoing' WHERE Id=@Id", new { Id = resumeId });
                    var updated = conn.QueryFirstOrDefault<Resume>("SELECT Status FROM Resumes WHERE Id=@Id", new { Id = resumeId });
                    if (updated.Status != "Ongoing") throw new Exception("Resume status update failed.");
                }
                Console.WriteLine("[PASS] Resume Management Operations");

                Console.WriteLine("\n✅ ALL TESTS PASSED SUCCESSFULLY!");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ TEST FAILED: {ex.Message}\n{ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}
