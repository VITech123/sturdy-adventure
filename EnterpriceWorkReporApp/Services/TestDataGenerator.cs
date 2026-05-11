using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dapper;
using EnterpriseWorkReport.Models;

namespace EnterpriseWorkReport.Services
{
    /// <summary>
    /// Comprehensive test data generator for Enterprise Work Report application
    /// Generates realistic test data for all features
    /// </summary>
    public static class TestDataGenerator
    {
        private static Random _random = new Random();
        
        // Test data collections
        private static readonly string[] FirstNames = { "Rajesh", "Priya", "Amit", "Sunita", "Vikram", "Anita", "Suresh", "Deepa", "Rahul", "Neha", "Arun", "Pooja", "Kiran", "Meera", "Sanjay" };
        private static readonly string[] LastNames = { "Kumar", "Sharma", "Patel", "Singh", "Gupta", "Reddy", "Nair", "Desai", "Iyer", "Joshi", "Shah", "Mehta", "Verma", "Rao", "Malhotra" };
        private static readonly string[] Departments = { "IT", "HR", "Finance", "Operations", "Sales", "Marketing", "QA", "Design" };
        private static readonly string[] Designations = { "Junior Associate", "Associate", "Senior Associate", "Team Lead", "Manager", "Senior Manager", "Director" };
        private static readonly string[] ProjectPrefixes = { "Data Entry", "Content", "Research", "Analysis", "Documentation", "Translation", "Verification" };
        private static readonly string[] ProjectSuffixes = { "Project Alpha", "Portal", "System", "Platform", "Suite", "Hub", "Center" };
        private static readonly string[] LeaveReasons = { "Family function", "Medical appointment", "Personal work", "Vacation", "Emergency", "Childcare", "Bereavement" };
        private static readonly string[] QualityRemarks = { "Excellent work", "Good accuracy", "Needs improvement", "Satisfactory", "Outstanding performance", "Met expectations" };
        private static readonly string[] MessageSubjects = { "Project Update", "Meeting Reminder", "Deadline Notification", "Welcome to the team", "Policy Update", "System Maintenance" };
        
        public static void GenerateAllTestData()
        {
            Console.WriteLine("=== Starting Comprehensive Test Data Generation ===\n");
            
            try
            {
                // Ensure database is initialized
                DatabaseService.InitializeDatabase();
                
                // 0. Build clean slate
                ClearAllData();
                
                // 1. Generate Users (Admin already exists)
                var userIds = GenerateUsers(15);
                Console.WriteLine($"[✓] Generated {userIds.Count} users");
                
                // 2. Generate Projects
                var projectIds = GenerateProjects(8);
                Console.WriteLine($"[✓] Generated {projectIds.Count} projects");
                
                // 3. Generate Project Fields for each project
                GenerateProjectFields(projectIds);
                Console.WriteLine($"[✓] Generated project fields");
                
                // 4. Generate Work Reports
                int reportCount = GenerateWorkReports(userIds, projectIds, 150);
                Console.WriteLine($"[✓] Generated {reportCount} work reports");
                
                // 5. Generate Attendance Records
                int attendanceCount = GenerateAttendance(userIds, 60);
                Console.WriteLine($"[✓] Generated {attendanceCount} attendance records");
                
                // 6. Generate Leave Requests
                int leaveCount = GenerateLeaveRequests(userIds, 40);
                Console.WriteLine($"[✓] Generated {leaveCount} leave requests");
                
                // 7. Generate Quality Reports
                int qualityCount = GenerateQualityReports(userIds, 80);
                Console.WriteLine($"[✓] Generated {qualityCount} quality reports");
                
                // 8. Generate Messages
                int messageCount = GenerateMessages(userIds, 60);
                Console.WriteLine($"[✓] Generated {messageCount} messages");
                
                // 9. Generate Audit Logs
                int auditCount = GenerateAuditLogs(userIds, 50);
                Console.WriteLine($"[✓] Generated {auditCount} audit logs");

                // 10. Setup Actual Demo Data (D:\demodata)
                SetupActualDemoData();
                Console.WriteLine($"[✓] Configured actual demo projects (Gamma142, Gamma143)");
                
                Console.WriteLine("\n=== Test Data Generation Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[✗] Error generating test data: {ex.Message}");
                throw;
            }
        }

        public static void ClearAllData()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                // Order matters for FK constraints: Delete children first
                conn.Execute("DELETE FROM AuditLogs");
                conn.Execute("DELETE FROM Messages");
                conn.Execute("DELETE FROM QualityReports");
                conn.Execute("DELETE FROM Leaves");
                conn.Execute("DELETE FROM Attendance");
                conn.Execute("DELETE FROM BillingSummary"); // Move up (depends on Projects/Users)
                conn.Execute("DELETE FROM WorkReportItems");
                conn.Execute("DELETE FROM WorkReports");
                conn.Execute("DELETE FROM MasterData");
                conn.Execute("DELETE FROM ProjectFields");
                conn.Execute("DELETE FROM Projects");
                // Keep admin user, delete others
                conn.Execute("DELETE FROM Users WHERE Username != 'admin'");
            }
        }
        
        private static List<int> GenerateUsers(int count)
        {
            var userIds = new List<int>();
            
            using (var conn = DatabaseService.GetConnection())
            {
                // Clear existing test users (keep admin)
                conn.Execute("DELETE FROM WorkReportItems WHERE WorkReportId IN (SELECT Id FROM WorkReports WHERE UserId IN (SELECT Id FROM Users WHERE Username LIKE 'user%'))");
                conn.Execute("DELETE FROM WorkReports WHERE UserId IN (SELECT Id FROM Users WHERE Username LIKE 'user%')");
                conn.Execute("DELETE FROM Attendance WHERE UserId IN (SELECT Id FROM Users WHERE Username LIKE 'user%')");
                conn.Execute("DELETE FROM Leaves WHERE UserId IN (SELECT Id FROM Users WHERE Username LIKE 'user%')");
                conn.Execute("DELETE FROM QualityReports WHERE UserId IN (SELECT Id FROM Users WHERE Username LIKE 'user%')");
                conn.Execute("DELETE FROM Messages WHERE SenderId IN (SELECT Id FROM Users WHERE Username LIKE 'user%') OR ReceiverId IN (SELECT Id FROM Users WHERE Username LIKE 'user%')");
                conn.Execute("DELETE FROM AuditLogs WHERE UserId IN (SELECT Id FROM Users WHERE Username LIKE 'user%')");
                conn.Execute("DELETE FROM Users WHERE Username LIKE 'user%'");
                
                for (int i = 1; i <= count; i++)
                {
                    string firstName = FirstNames[_random.Next(FirstNames.Length)];
                    string lastName = LastNames[_random.Next(LastNames.Length)];
                    string fullName = $"{firstName} {lastName}";
                    string username = $"user{i}";
                    string email = $"{firstName.ToLower()}.{lastName.ToLower()}@company.com";
                    string phone = $"+91 {_random.Next(70000, 99999)} {_random.Next(10000, 99999)}";
                    string department = Departments[_random.Next(Departments.Length)];
                    string designation = Designations[_random.Next(Designations.Length)];
                    string passwordHash = AuthService.HashPassword("vendhan@123");
                    bool isActive = _random.Next(10) < 9; // 90% active
                    
                    string sql = @"
                        INSERT INTO Users (Username, PasswordHash, Role, FullName, Email, Phone, Department, Designation, IsActive, CreatedAt, LastLoginAt)
                        VALUES (@Username, @PasswordHash, 'User', @FullName, @Email, @Phone, @Department, @Designation, @IsActive, @CreatedAt, @LastLoginAt)
                        RETURNING Id;";
                    
                    var createdAt = DateTime.Now.AddDays(-_random.Next(1, 365));
                    var lastLoginAt = _random.Next(2) == 0 ? (DateTime?)createdAt.AddDays(_random.Next(30)) : null;
                    
                    int userId = Convert.ToInt32(conn.QuerySingle<long>(sql, new
                    {
                        Username = username,
                        PasswordHash = passwordHash,
                        FullName = fullName,
                        Email = email,
                        Phone = phone,
                        Department = department,
                        Designation = designation,
                        IsActive = isActive ? 1 : 0,
                        CreatedAt = createdAt,
                        LastLoginAt = lastLoginAt
                    }));
                    
                    userIds.Add(userId);
                }
            }
            
            return userIds;
        }
        
        private static List<int> GenerateProjects(int count)
        {
            var projectIds = new List<int>();
            
            using (var conn = DatabaseService.GetConnection())
            {
                // Clear existing test projects
                conn.Execute("DELETE FROM ProjectFields WHERE ProjectId IN (SELECT Id FROM Projects WHERE Name LIKE 'Test%')");
                conn.Execute("DELETE FROM Projects WHERE Name LIKE 'Test%'");
                
                for (int i = 1; i <= count; i++)
                {
                    string prefix = ProjectPrefixes[_random.Next(ProjectPrefixes.Length)];
                    string suffix = ProjectSuffixes[_random.Next(ProjectSuffixes.Length)];
                    string name = $"Test {prefix} {suffix} {i}";
                    string description = $"This is a test project for {prefix.ToLower()} work. It includes various tasks and deliverables.";
                    bool isActive = _random.Next(10) < 8; // 80% active
                    
                    // Different billing formulas for variety
                    string[] formulas = {
                        "(WordCount / 1000) * Rate",
                        "Pages * RatePerPage",
                        "Hours * HourlyRate",
                        "(Records * RatePerRecord) + Bonus",
                        "Units * UnitPrice"
                    };
                    string formula = formulas[_random.Next(formulas.Length)];
                    
                    string sql = @"
                        INSERT INTO Projects (Name, Description, IsActive, BillingFormula)
                        VALUES (@Name, @Description, @IsActive, @BillingFormula)
                        RETURNING Id;";
                    
                    int projectId = Convert.ToInt32(conn.QuerySingle<long>(sql, new
                    {
                        Name = name,
                        Description = description,
                        IsActive = isActive ? 1 : 0,
                        BillingFormula = formula
                    }));
                    
                    projectIds.Add(projectId);
                }
            }
            
            return projectIds;
        }
        
        private static void GenerateProjectFields(List<int> projectIds)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                foreach (int projectId in projectIds)
                {
                    // Get project formula to determine fields needed
                    var formula = conn.QuerySingle<string>("SELECT BillingFormula FROM Projects WHERE Id = @Id", new { Id = projectId });
                    
                    var fields = new List<(string label, string type, bool required, bool billing)>();
                    
                    // Add fields based on formula
                    if (formula.Contains("WordCount"))
                    {
                        fields.Add(("WordCount", "Number", true, true));
                        fields.Add(("Rate", "Number", true, true));
                    }
                    if (formula.Contains("Pages"))
                    {
                        fields.Add(("Pages", "Number", true, true));
                        fields.Add(("RatePerPage", "Number", true, true));
                    }
                    if (formula.Contains("Hours"))
                    {
                        fields.Add(("Hours", "Number", true, true));
                        fields.Add(("HourlyRate", "Number", true, true));
                    }
                    if (formula.Contains("Records"))
                    {
                        fields.Add(("Records", "Number", true, true));
                        fields.Add(("RatePerRecord", "Number", true, true));
                        fields.Add(("Bonus", "Number", false, true));
                    }
                    if (formula.Contains("Units"))
                    {
                        fields.Add(("Units", "Number", true, true));
                        fields.Add(("UnitPrice", "Number", true, true));
                    }
                    
                    // Add common fields
                    fields.Add(("ObjectID", "Text", true, false));
                    fields.Add(("Notes", "Text", false, false));
                    
                    int sortOrder = 1;
                    foreach (var field in fields)
                    {
                        conn.Execute(@"
                            INSERT INTO ProjectFields (ProjectId, FieldLabel, FieldType, IsRequired, IncludeInBilling, SortOrder)
                            VALUES (@ProjectId, @FieldLabel, @FieldType, @IsRequired, @IncludeInBilling, @SortOrder)",
                            new
                            {
                                ProjectId = projectId,
                                FieldLabel = field.label,
                                FieldType = field.type,
                                IsRequired = field.required ? 1 : 0,
                                IncludeInBilling = field.billing ? 1 : 0,
                                SortOrder = sortOrder++
                            });
                    }
                }
            }
        }
        
        private static int GenerateWorkReports(List<int> userIds, List<int> projectIds, int count)
        {
            int generatedCount = 0;
            
            using (var conn = DatabaseService.GetConnection())
            {
                for (int i = 0; i < count; i++)
                {
                    int userId = userIds[_random.Next(userIds.Count)];
                    int projectId = projectIds[_random.Next(projectIds.Count)];
                    
                    // Random date within last 60 days
                    DateTime submissionDate = DateTime.Now.AddDays(-_random.Next(60)).Date;
                    string objectId = $"OBJ-{DateTime.Now.Year}-{_random.Next(10000, 99999)}";
                    
                    // Calculate billing amount based on project formula
                    var fields = conn.Query<(int Id, string FieldLabel, string FieldType, bool IncludeInBilling)>(
                        "SELECT Id, FieldLabel, FieldType, IncludeInBilling FROM ProjectFields WHERE ProjectId = @ProjectId",
                        new { ProjectId = projectId }).ToList();
                    
                    double billingAmount = 0;
                    var fieldValues = new Dictionary<string, string>();
                    
                    foreach (var field in fields)
                    {
                        string value = "";
                        if (field.FieldLabel == "ObjectID")
                            value = objectId;
                        else if (field.FieldType == "Number")
                        {
                            value = _random.Next(1, 1000).ToString();
                            fieldValues[field.FieldLabel] = value;
                        }
                        else
                            value = $"Sample {field.FieldLabel}";
                    }
                    
                    // Simple billing calculation (for testing)
                    if (fieldValues.ContainsKey("WordCount") && fieldValues.ContainsKey("Rate"))
                    {
                        billingAmount = (double.Parse(fieldValues["WordCount"]) / 1000) * double.Parse(fieldValues["Rate"]);
                    }
                    else if (fieldValues.ContainsKey("Pages") && fieldValues.ContainsKey("RatePerPage"))
                    {
                        billingAmount = double.Parse(fieldValues["Pages"]) * double.Parse(fieldValues["RatePerPage"]);
                    }
                    else if (fieldValues.ContainsKey("Hours") && fieldValues.ContainsKey("HourlyRate"))
                    {
                        billingAmount = double.Parse(fieldValues["Hours"]) * double.Parse(fieldValues["HourlyRate"]);
                    }
                    else
                    {
                        billingAmount = _random.Next(100, 5000);
                    }
                    
                    // Insert work report
                    int workReportId = Convert.ToInt32(conn.QuerySingle<long>(@"
                        INSERT INTO WorkReports (ProjectId, UserId, SubmissionDate, ObjectId, BillingAmount)
                        VALUES (@ProjectId, @UserId, @SubmissionDate, @ObjectId, @BillingAmount)
                        RETURNING Id;",
                        new { ProjectId = projectId, UserId = userId, SubmissionDate = submissionDate, ObjectId = objectId, BillingAmount = billingAmount }));
                    
                    // Insert work report items
                    foreach (var field in fields)
                    {
                        string value = "";
                        if (field.FieldLabel == "ObjectID")
                            value = objectId;
                        else if (fieldValues.ContainsKey(field.FieldLabel))
                            value = fieldValues[field.FieldLabel];
                        else if (field.FieldType == "Number")
                            value = _random.Next(1, 1000).ToString();
                        else
                            value = $"Sample {field.FieldLabel}";
                        
                        conn.Execute(@"
                            INSERT INTO WorkReportItems (WorkReportId, FieldId, Value)
                            VALUES (@WorkReportId, @FieldId, @Value)",
                            new { WorkReportId = workReportId, FieldId = field.Id, Value = value });
                    }
                    
                    generatedCount++;
                }
            }
            
            return generatedCount;
        }
        
        private static int GenerateAttendance(List<int> userIds, int daysBack)
        {
            int generatedCount = 0;
            string[] statuses = { "Present", "Present", "Present", "Present", "Present", "Absent", "Half Day", "On Leave", "Work From Home" };
            
            using (var conn = DatabaseService.GetConnection())
            {
                foreach (int userId in userIds)
                {
                    for (int day = 0; day < daysBack; day++)
                    {
                        // Skip weekends
                        DateTime date = DateTime.Now.AddDays(-day).Date;
                        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                            continue;
                        
                        string status = statuses[_random.Next(statuses.Length)];
                        string remarks = status == "Present" ? "" : $"Auto-generated {status.ToLower()} record";
                        
                        conn.Execute(@"
                            INSERT INTO Attendance (UserId, Date, Status, Remarks)
                            VALUES (@UserId, @Date, @Status, @Remarks)
                            ON CONFLICT (UserId, Date) DO NOTHING",
                            new { UserId = userId, Date = date, Status = status, Remarks = remarks });
                        
                        generatedCount++;
                    }
                }
            }
            
            return generatedCount;
        }
        
        private static int GenerateLeaveRequests(List<int> userIds, int count)
        {
            int generatedCount = 0;
            string[] leaveTypes = { "Casual Leave", "Sick Leave", "Earned Leave", "Maternity Leave", "Paternity Leave", "Unpaid Leave" };
            string[] statuses = { "Pending", "Pending", "Approved", "Approved", "Approved", "Rejected" };
            
            using (var conn = DatabaseService.GetConnection())
            {
                for (int i = 0; i < count; i++)
                {
                    int userId = userIds[_random.Next(userIds.Count)];
                    string leaveType = leaveTypes[_random.Next(leaveTypes.Length)];
                    string status = statuses[_random.Next(statuses.Length)];
                    string reason = LeaveReasons[_random.Next(LeaveReasons.Length)];
                    
                    // Random start date within next 30 days or past 30 days
                    DateTime startDate = DateTime.Now.AddDays(_random.Next(-30, 30)).Date;
                    int duration = _random.Next(1, 6); // 1-5 days
                    DateTime endDate = startDate.AddDays(duration - 1);
                    
                    conn.Execute(@"
                        INSERT INTO Leaves (UserId, LeaveType, StartDate, EndDate, Reason, Status)
                        VALUES (@UserId, @LeaveType, @StartDate, @EndDate, @Reason, @Status)",
                        new { UserId = userId, LeaveType = leaveType, StartDate = startDate, EndDate = endDate, Reason = reason, Status = status });
                    
                    generatedCount++;
                }
            }
            
            return generatedCount;
        }
        
        private static int GenerateQualityReports(List<int> userIds, int count)
        {
            int generatedCount = 0;
            
            using (var conn = DatabaseService.GetConnection())
            {
                for (int i = 0; i < count; i++)
                {
                    int userId = userIds[_random.Next(userIds.Count)];
                    DateTime reportDate = DateTime.Now.AddDays(-_random.Next(60)).Date;
                    
                    double accuracy = _random.Next(85, 100);
                    double errorRate = Math.Round(100 - accuracy + _random.NextDouble() * 2, 2);
                    int reworkCount = _random.Next(0, 5);
                    double qualityScore = Math.Round(accuracy - (errorRate * 0.5) - (reworkCount * 0.5), 2);
                    string remarks = QualityRemarks[_random.Next(QualityRemarks.Length)];
                    
                    conn.Execute(@"
                        INSERT INTO QualityReports (UserId, ReportDate, Accuracy, ErrorRate, ReworkCount, QualityScore, Remarks)
                        VALUES (@UserId, @ReportDate, @Accuracy, @ErrorRate, @ReworkCount, @QualityScore, @Remarks)",
                        new { UserId = userId, ReportDate = reportDate, Accuracy = accuracy, ErrorRate = errorRate, ReworkCount = reworkCount, QualityScore = qualityScore, Remarks = remarks });
                    
                    generatedCount++;
                }
            }
            
            return generatedCount;
        }
        
        private static int GenerateMessages(List<int> userIds, int count)
        {
            int generatedCount = 0;
            
            using (var conn = DatabaseService.GetConnection())
            {
                // Get admin user
                int adminId = Convert.ToInt32(conn.QuerySingle<long>("SELECT Id FROM Users WHERE Username = 'admin'"));
                
                for (int i = 0; i < count; i++)
                {
                    bool isBroadcast = _random.Next(10) < 2; // 20% broadcast
                    int senderId = _random.Next(2) == 0 ? adminId : userIds[_random.Next(userIds.Count)];
                    int? receiverId = isBroadcast ? null : (senderId == adminId ? userIds[_random.Next(userIds.Count)] : adminId);
                    
                    string subject = MessageSubjects[_random.Next(MessageSubjects.Length)];
                    string content = $"This is a test message regarding {subject.ToLower()}. Please review and respond accordingly.";
                    bool isRead = _random.Next(2) == 1;
                    
                    conn.Execute(@"
                        INSERT INTO Messages (SenderId, ReceiverId, Subject, Content, IsRead, IsBroadcast, CreatedAt)
                        VALUES (@SenderId, @ReceiverId, @Subject, @Content, @IsRead, @IsBroadcast, @CreatedAt)",
                        new
                        {
                            SenderId = senderId,
                            ReceiverId = receiverId,
                            Subject = subject,
                            Content = content,
                            IsRead = isRead ? 1 : 0,
                            IsBroadcast = isBroadcast ? 1 : 0,
                            CreatedAt = DateTime.Now.AddDays(-_random.Next(30))
                        });
                    
                    generatedCount++;
                }
            }
            
            return generatedCount;
        }
        
        private static int GenerateAuditLogs(List<int> userIds, int count)
        {
            int generatedCount = 0;
            string[] actions = { "User Login", "Work Report Created", "Attendance Marked", "Leave Requested", "Profile Updated", "Message Sent" };
            
            using (var conn = DatabaseService.GetConnection())
            {
                // Get admin user
                int adminId = Convert.ToInt32(conn.QuerySingle<long>("SELECT Id FROM Users WHERE Username = 'admin'"));
                var allUserIds = new List<int>(userIds) { adminId };
                
                for (int i = 0; i < count; i++)
                {
                    int userId = allUserIds[_random.Next(allUserIds.Count)];
                    string action = actions[_random.Next(actions.Length)];
                    string details = $"Test audit log entry for {action.ToLower()} by user {userId}";
                    
                    // Get username
                    string username = conn.QuerySingle<string>("SELECT Username FROM Users WHERE Id = @Id", new { Id = userId });
                    
                    conn.Execute(@"
                        INSERT INTO AuditLogs (UserId, Username, Action, Details, Timestamp)
                        VALUES (@UserId, @Username, @Action, @Details, @Timestamp)",
                        new
                        {
                            UserId = userId,
                            Username = username,
                            Action = action,
                            Details = details,
                            Timestamp = DateTime.Now.AddDays(-_random.Next(30))
                        });
                    
                    generatedCount++;
                }
            }
            
            return generatedCount;
        }

        /// <summary>
        /// Configures the actual demo projects and settings provided by the user.
        /// </summary>
        public static void SetupActualDemoData()
        {
            string root = @"D:\demodata";
            string masterNamesPath = Path.Combine(root, "MasterNames.xlsx");

            using (var conn = DatabaseService.GetConnection())
            {
                // 1. Configure Global Settings
                conn.Execute("UPDATE CompanySettings SET MasterNamesPath = @P WHERE Id = 1", 
                    new { P = masterNamesPath });

                // 2. Create Users from MasterNames.xlsx
                var matcher = new FuzzyNameMatcher();
                matcher.LoadMasterNames(masterNamesPath);
                if (matcher.IsLoaded)
                {
                    foreach (var name in matcher.GetMasterNames())
                    {
                        // Check if user already exists
                        bool exists = conn.ExecuteScalar<bool>("SELECT COUNT(1) FROM Users WHERE FullName = @N", new { N = name });
                        if (!exists)
                        {
                            string username = name.Replace(".", "").Replace(" ", "").ToLower();
                            // Ensure unique username
                            if (conn.ExecuteScalar<bool>("SELECT COUNT(1) FROM Users WHERE Username = @U", new { U = username }))
                                username += _random.Next(100, 999);

                            conn.Execute(@"
                                INSERT INTO Users (Username, PasswordHash, Role, FullName, Email, IsActive, CreatedAt)
                                VALUES (@U, @P, 'User', @N, @E, 1, CURRENT_TIMESTAMP)",
                                new { 
                                    U = username, 
                                    P = AuthService.HashPassword("vendhan@123"), 
                                    N = name,
                                    E = $"{username}@company.com"
                                });
                        }
                    }
                }

                // 3. Create Projects
                var projects = new[]
                {
                    new { 
                        Name = "Gamma142", 
                        Master = @"\\vendhansvr1\e$\Gamma142.xlsx", 
                        Pass = "142", 
                        Quality = @"\\vendhansvr1\e$\Gamma142\Quality_Report",
                        Formula = "(charactercount / 1000) * 4.95",
                        Fields = new[] { ("object_Id", "Text", true, false), ("charactercount", "Number", true, true) }
                    },
                    new { 
                        Name = "Gamma143", 
                        Master = @"\\vendhansvr1\e$\Gamma143.xlsx", 
                        Pass = "143", 
                        Quality = @"\\vendhansvr1\e$\Gamma143\Quality_Report",
                        Formula = "((pages * 0.05) + ((charactercount / 1000) * 4.95))",
                        Fields = new[] { ("object_Id", "Text", true, false), ("pages", "Number", true, true), ("charactercount", "Number", true, true) }
                    }
                };

                foreach (var p in projects)
                {
                    // Check if exists
                    int? projectId = conn.QueryFirstOrDefault<int?>("SELECT Id FROM Projects WHERE Name = @N", new { N = p.Name });
                    
                    if (projectId.HasValue)
                    {
                        conn.Execute(@"UPDATE Projects SET MasterFilePath=@M, MasterFilePassword=@P, QualityBasePath=@Q, BillingFormula=@F WHERE Id=@Id",
                            new { M = p.Master, P = p.Pass, Q = p.Quality, F = p.Formula, Id = projectId.Value });
                    }
                    else
                    {
                        projectId = Convert.ToInt32(conn.QuerySingle<long>(@"INSERT INTO Projects (Name, Description, IsActive, MasterFilePath, MasterFilePassword, QualityBasePath, BillingFormula)
                                       VALUES (@N, @D, 1, @M, @P, @Q, @F) RETURNING Id",
                            new { N = p.Name, D = $"Project {p.Name} live data", M = p.Master, P = p.Pass, Q = p.Quality, F = p.Formula }));
                    }

                    // Setup Fields
                    conn.Execute("DELETE FROM ProjectFields WHERE ProjectId = @Id", new { Id = projectId });
                    int sortOrder = 1;
                    foreach (var field in p.Fields)
                    {
                        conn.Execute(@"INSERT INTO ProjectFields (ProjectId, FieldLabel, FieldType, IsRequired, IncludeInBilling, SortOrder)
                                       VALUES (@ProjectId, @Label, @Type, @Req, @Bill, @Sort)",
                            new { ProjectId = projectId, Label = field.Item1, Type = field.Item2, Req = field.Item3 ? 1 : 0, Bill = field.Item4 ? 1 : 0, Sort = sortOrder++ });
                    }
                }
            }
        }
    }
}
