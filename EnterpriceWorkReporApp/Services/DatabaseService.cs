using System;
using System.IO;
using Npgsql;
using Dapper;

namespace EnterpriseWorkReport.Services
{
    public static class DatabaseService
    {
        public static string AppDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data");
        public static string DbPath = Path.Combine(AppDataFolder, "database.db");
        public static string BackupsFolder = Path.Combine(AppDataFolder, "backups");
        
        // PostgreSQL connection string
        public static string ConnectionString = "Host=localhost;Port=5432;Database=vendhan;Username=postgres;Password=vendhan@123;Pooling=true;Maximum Pool Size=100;";

        public static void InitializeDatabase()
        {
            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);
            
            if (!Directory.Exists(BackupsFolder))
                Directory.CreateDirectory(BackupsFolder);

            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                connection.Open();
                CreateTables(connection);
                SeedInitialAdmin(connection);
                SeedProjectFormulas(connection);
            }
        }

        private static void SeedProjectFormulas(NpgsqlConnection connection)
        {
            connection.Execute(@"
                UPDATE Projects SET BillingFormula = '(charactercount/1000)*4.95' WHERE Name ILIKE '%Gamma%142%';
                UPDATE Projects SET BillingFormula = '((pages*0.05)+((charactercount/1000)*4.95))' WHERE Name ILIKE '%Gamma%143%';
            ");
        }

        public static NpgsqlConnection GetConnection()
        {
            var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static void UpdateProjectSyncStatus(int projectId, DateTime syncAt, string status)
        {
            try
            {
                using var conn = GetConnection();
                conn.Execute(@"UPDATE Projects SET LastMasterSyncAt = @SyncAt, LastMasterSyncStatus = @Status WHERE Id = @Id", 
                    new { SyncAt = syncAt, Status = status, Id = projectId });
            }
            catch { }
        }

        private static void CreateTables(NpgsqlConnection connection)
        {
            // Migrations
            try { connection.Execute("ALTER TABLE MasterData ADD COLUMN IF NOT EXISTS ExtraData TEXT;"); } catch { }
            try { connection.Execute("ALTER TABLE MasterData ADD COLUMN IF NOT EXISTS ProjectId INTEGER;"); } catch { }
            try { connection.Execute("ALTER TABLE QualityReports ADD COLUMN IF NOT EXISTS ProjectId INTEGER;"); } catch { }
            try { connection.Execute("ALTER TABLE QualityReports ADD COLUMN IF NOT EXISTS IsDetailed INTEGER DEFAULT 0;"); } catch { }
            try { connection.Execute("ALTER TABLE Projects ADD COLUMN IF NOT EXISTS MasterFilePassword TEXT;"); } catch { }
            try { connection.Execute("ALTER TABLE Projects ADD COLUMN IF NOT EXISTS LastMasterSyncAt TIMESTAMP;"); } catch { }
            try { connection.Execute("ALTER TABLE Projects ADD COLUMN IF NOT EXISTS LastMasterSyncStatus TEXT;"); } catch { }

            string createUsersTableQuery = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id SERIAL PRIMARY KEY,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    IsActive INTEGER DEFAULT 1,
                    Email TEXT,
                    Phone TEXT,
                    Department TEXT,
                    Designation TEXT,
                    ProfilePicture TEXT,
                    WallpaperPath TEXT,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    LastLoginAt TIMESTAMP
                );";

            string createProjectsTableQuery = @"
                CREATE TABLE IF NOT EXISTS Projects (
                    Id SERIAL PRIMARY KEY,
                    ProjectCode TEXT UNIQUE,
                    Name TEXT NOT NULL UNIQUE,
                    Description TEXT,
                    IsActive INTEGER DEFAULT 1,
                    BillingFormula TEXT,
                    MasterFilePath TEXT,
                    MasterFilePassword TEXT,
                    QualityBasePath TEXT,
                    LastMasterSyncAt TIMESTAMP,
                    LastMasterSyncStatus TEXT
                );";

            string createProjectFieldsTableQuery = @"
                CREATE TABLE IF NOT EXISTS ProjectFields (
                    Id SERIAL PRIMARY KEY,
                    ProjectId INTEGER NOT NULL,
                    InternalFieldName TEXT,
                    FieldLabel TEXT NOT NULL,
                    FieldType TEXT NOT NULL,
                    IsRequired INTEGER DEFAULT 0,
                    IncludeInBilling INTEGER DEFAULT 0,
                    Instructions TEXT,
                    SortOrder INTEGER DEFAULT 0,
                    FOREIGN KEY(ProjectId) REFERENCES Projects(Id)
                );";

            string createBillingSummaryTableQuery = @"
                CREATE TABLE IF NOT EXISTS BillingSummary (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER NOT NULL,
                    ProjectId INTEGER NOT NULL,
                    Month TEXT,
                    TotalObjects INTEGER DEFAULT 0,
                    TotalPages INTEGER DEFAULT 0,
                    TotalCharacters INTEGER DEFAULT 0,
                    TotalAmount REAL DEFAULT 0,
                    LastUpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(UserId) REFERENCES Users(Id),
                    FOREIGN KEY(ProjectId) REFERENCES Projects(Id),
                    UNIQUE(UserId, ProjectId, Month)
                );";

            string createWorkReportsTableQuery = @"
                CREATE TABLE IF NOT EXISTS WorkReports (
                    Id SERIAL PRIMARY KEY,
                    ProjectId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    SubmissionDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    ObjectId TEXT NOT NULL, 
                    BillingAmount REAL DEFAULT 0,
                    Status TEXT DEFAULT 'Submitted',
                    AttachmentPath TEXT,
                    AdminNote TEXT,
                    ExtraFields TEXT,
                    FOREIGN KEY(ProjectId) REFERENCES Projects(Id),
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );";
            string createWorkReportItemsTableQuery = @"
                CREATE TABLE IF NOT EXISTS WorkReportItems (
                    Id SERIAL PRIMARY KEY,
                    WorkReportId INTEGER NOT NULL,
                    FieldId INTEGER NOT NULL,
                    Value TEXT,
                    FOREIGN KEY(WorkReportId) REFERENCES WorkReports(Id),
                    FOREIGN KEY(FieldId) REFERENCES ProjectFields(Id)
                );";

            string createAttendanceTableQuery = @"
                CREATE TABLE IF NOT EXISTS Attendance (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER NOT NULL,
                    Date DATE NOT NULL,
                    Status TEXT NOT NULL,
                    Remarks TEXT,
                    ClockInTime TEXT,
                    ClockOutTime TEXT,
                    HoursWorked REAL DEFAULT 0,
                    FOREIGN KEY(UserId) REFERENCES Users(Id),
                    UNIQUE(UserId, Date)
                );";

            string createLeavesTableQuery = @"
                CREATE TABLE IF NOT EXISTS Leaves (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER NOT NULL,
                    LeaveType TEXT NOT NULL,
                    StartDate DATE NOT NULL,
                    EndDate DATE NOT NULL,
                    Reason TEXT,
                    Status TEXT DEFAULT 'Pending',
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );";

            string createQualityReportsTable = @"
                CREATE TABLE IF NOT EXISTS QualityReports (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER NOT NULL,
                    ProjectId INTEGER,
                    ReportDate DATE NOT NULL,
                    Accuracy REAL DEFAULT 0,
                    ErrorRate REAL DEFAULT 0,
                    ReworkCount INTEGER DEFAULT 0,
                    QualityScore REAL DEFAULT 0,
                    IsDetailed INTEGER DEFAULT 0,
                    Remarks TEXT,
                    FOREIGN KEY(UserId) REFERENCES Users(Id),
                    FOREIGN KEY(ProjectId) REFERENCES Projects(Id)
                );";

            string createQualityReportItemsTable = @"
                CREATE TABLE IF NOT EXISTS QualityReportItems (
                    Id SERIAL PRIMARY KEY,
                    ReportId INTEGER NOT NULL,
                    ObjectId TEXT,
                    AssignedName TEXT,
                    Process TEXT,
                    Articles INTEGER DEFAULT 0,
                    KeyedCharacters INTEGER DEFAULT 0,
                    ActualCharacters INTEGER DEFAULT 0,
                    DefectCharacters INTEGER DEFAULT 0,
                    Quality REAL DEFAULT 0,
                    FOREIGN KEY(ReportId) REFERENCES QualityReports(Id)
                );";

            string createAuditLogsTable = @"
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER,
                    Username TEXT,
                    Action TEXT NOT NULL,
                    Details TEXT,
                    Timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );";

            string createMessagesTable = @"
                CREATE TABLE IF NOT EXISTS Messages (
                    Id SERIAL PRIMARY KEY,
                    SenderId INTEGER NOT NULL,
                    ReceiverId INTEGER,
                    Subject TEXT,
                    Content TEXT NOT NULL,
                    AttachmentPath TEXT,
                    AttachmentType TEXT,
                    IsRead INTEGER DEFAULT 0,
                    IsBroadcast INTEGER DEFAULT 0,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(SenderId) REFERENCES Users(Id),
                    FOREIGN KEY(ReceiverId) REFERENCES Users(Id)
                );
                
                CREATE INDEX IF NOT EXISTS idx_messages_receiver ON Messages(ReceiverId);
                CREATE INDEX IF NOT EXISTS idx_messages_sender ON Messages(SenderId);";

            string createMasterDataTable = @"
                CREATE TABLE IF NOT EXISTS MasterData (
                    Id SERIAL PRIMARY KEY,
                    ProjectId INTEGER NOT NULL,
                    ManifestId TEXT,
                    ObjectId TEXT NOT NULL,
                    ObjectName TEXT,
                    Pages INTEGER DEFAULT 0,
                    Articles INTEGER DEFAULT 0,
                    CharacterCount INTEGER DEFAULT 0,
                    StartDate DATE,
                    EndDate DATE,
                    Status TEXT DEFAULT 'Unassigned',
                    Batch TEXT,
                    QualityScore REAL DEFAULT 0,
                    AssignedUserId INTEGER,
                    AssignedUserName TEXT,
                    ErrorDetails TEXT,
                    ExtraData TEXT,
                    ImportDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(ProjectId) REFERENCES Projects(Id),
                    FOREIGN KEY(AssignedUserId) REFERENCES Users(Id)
                );
                
                CREATE INDEX IF NOT EXISTS idx_masterdata_project ON MasterData(ProjectId);
                CREATE INDEX IF NOT EXISTS idx_masterdata_status ON MasterData(Status);
                CREATE INDEX IF NOT EXISTS idx_masterdata_manifest ON MasterData(ManifestId);
                CREATE UNIQUE INDEX IF NOT EXISTS idx_masterdata_project_object ON MasterData(ProjectId, ObjectId);
                DROP INDEX IF EXISTS idx_masterdata_object;";

            string createCompanySettingsTable = @"
                CREATE TABLE IF NOT EXISTS CompanySettings (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    CompanyName TEXT DEFAULT 'My Company',
                    CompanyAddress TEXT,
                    CompanyPhone TEXT,
                    CompanyEmail TEXT,
                    TaxId TEXT,
                    CurrencySymbol TEXT DEFAULT '₹',
                    TimeZone TEXT DEFAULT 'India Standard Time',
                    LogoPath TEXT,
                    QualityReportsPath TEXT,
                    AttachmentsPath TEXT,
                    ProfilePicturesPath TEXT,
                    QualityThreshold REAL DEFAULT 85,
                    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );
                
                INSERT INTO CompanySettings (Id, CompanyName) VALUES (1, 'My Company') ON CONFLICT DO NOTHING;";

            string createUserSettingsTable = @"
                CREATE TABLE IF NOT EXISTS UserSettings (
                    UserId INTEGER NOT NULL,
                    Key TEXT NOT NULL,
                    Value TEXT,
                    PRIMARY KEY(UserId, Key),
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );";

            string createResumesTableQuery = @"
                CREATE TABLE IF NOT EXISTS ResumeFolders (
                    Id SERIAL PRIMARY KEY,
                    Name TEXT UNIQUE NOT NULL,
                    Icon TEXT
                );

                CREATE TABLE IF NOT EXISTS Resumes (
                    Id SERIAL PRIMARY KEY,
                    FolderId INTEGER,
                    CandidateName TEXT,
                    ContactNumber TEXT,
                    Email TEXT,
                    Experience TEXT,
                    ImagePath TEXT NOT NULL,
                    ThumbnailPath TEXT,
                    Status TEXT DEFAULT 'Pending',
                    ExtractedText TEXT,
                    UploadDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(FolderId) REFERENCES ResumeFolders(Id)
                );";

            using (var command = new NpgsqlCommand())
            {
                command.Connection = connection;

                command.CommandText = createUsersTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createProjectsTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createProjectFieldsTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createWorkReportsTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createWorkReportItemsTableQuery;
                command.ExecuteNonQuery();
                
                command.CommandText = createAttendanceTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createLeavesTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createQualityReportsTable;
                command.ExecuteNonQuery();

                command.CommandText = createQualityReportItemsTable;
                command.ExecuteNonQuery();

                command.CommandText = createAuditLogsTable;
                command.ExecuteNonQuery();

                command.CommandText = createBillingSummaryTableQuery;
                command.ExecuteNonQuery();
                
                command.CommandText = createMessagesTable;
                command.ExecuteNonQuery();
                
                command.CommandText = createMasterDataTable;
                command.ExecuteNonQuery();
                
                command.CommandText = createCompanySettingsTable;
                command.ExecuteNonQuery();

                command.CommandText = createUserSettingsTable;
                command.ExecuteNonQuery();

                command.CommandText = createResumesTableQuery;
                command.ExecuteNonQuery();
            }
            
            RunMigrations(connection);
        }
        
        private static void RunMigrations(NpgsqlConnection connection)
        {
            Action<string, string, string> addCol = (table, col, type) => {
                try {
                    using (var cmd = new NpgsqlCommand($"ALTER TABLE {table} ADD COLUMN {col} {type}", connection))
                        cmd.ExecuteNonQuery();
                } catch { }
            };

            addCol("Attendance", "ClockInTime", "TEXT");
            addCol("Attendance", "ClockOutTime", "TEXT");
            addCol("Attendance", "HoursWorked", "REAL DEFAULT 0");
            
            addCol("WorkReports", "AdminNote", "TEXT");
            addCol("WorkReports", "AttachmentPath", "TEXT");
            addCol("WorkReports", "ExtraFields", "TEXT");
            addCol("WorkReports", "Status", "TEXT DEFAULT 'Submitted'");

            addCol("Projects", "ProjectCode", "TEXT");
            addCol("Projects", "MasterFilePath", "TEXT");
            addCol("Projects", "QualityBasePath", "TEXT");
            addCol("Projects", "MasterFilePassword", "TEXT");

            addCol("ProjectFields", "InternalFieldName", "TEXT");
            addCol("ProjectFields", "Instructions", "TEXT");

            addCol("CompanySettings", "QualityReportsPath", "TEXT");
            addCol("CompanySettings", "AttachmentsPath", "TEXT");
            addCol("CompanySettings", "ProfilePicturesPath", "TEXT");
            addCol("CompanySettings", "QualityThreshold", "REAL DEFAULT 85");
            addCol("CompanySettings", "LateArrivalThreshold", "TEXT DEFAULT '09:30'");
            addCol("CompanySettings", "EarlyDepartureThreshold", "TEXT DEFAULT '18:00'");
            addCol("CompanySettings", "WorkStartTime", "TEXT DEFAULT '09:00'");
            addCol("CompanySettings", "WorkEndTime", "TEXT DEFAULT '18:00'");
            addCol("Users", "Place", "TEXT");
            addCol("Users", "Education", "TEXT");
            addCol("Users", "EmergencyNumber", "TEXT");
            addCol("Users", "JoinDate", "TIMESTAMP");
            addCol("Users", "RelievingDate", "TIMESTAMP");
            addCol("Users", "SelectedTheme", "TEXT");

            addCol("QualityReports", "ProjectId", "INTEGER");
            addCol("QualityReports", "IsDetailed", "INTEGER DEFAULT 0");
            addCol("MasterData", "Articles", "INTEGER DEFAULT 0");
            addCol("Resumes", "FolderId", "INTEGER");
            addCol("Resumes", "ThumbnailPath", "TEXT");
            
            try {
                using (var cmd = new NpgsqlCommand("CREATE UNIQUE INDEX IF NOT EXISTS idx_attendance_user_date ON Attendance(UserId, Date)", connection))
                    cmd.ExecuteNonQuery();
            } catch { }

            // Fix legacy role values: 'User' -> 'Employee'
            try {
                using (var cmd = new NpgsqlCommand("UPDATE Users SET Role = 'Employee' WHERE Role = 'User'", connection))
                    cmd.ExecuteNonQuery();
            } catch { }

            // Resume Indexes
            try {
                using (var cmd = new NpgsqlCommand("CREATE INDEX IF NOT EXISTS idx_resumes_status ON Resumes(Status)", connection))
                    cmd.ExecuteNonQuery();
                using (var cmd = new NpgsqlCommand("CREATE INDEX IF NOT EXISTS idx_resumes_name ON Resumes(CandidateName)", connection))
                    cmd.ExecuteNonQuery();
                using (var cmd = new NpgsqlCommand("CREATE INDEX IF NOT EXISTS idx_resumes_folder ON Resumes(FolderId)", connection))
                    cmd.ExecuteNonQuery();
            } catch { }

            // Seed Resume Folders
            try {
                string[] defaultFolders = { "Pending Resumes", "Relieved", "Current", "Work From Home" };
                foreach (var folder in defaultFolders) {
                    using (var cmd = new NpgsqlCommand("INSERT INTO ResumeFolders (Name) VALUES (@N) ON CONFLICT DO NOTHING", connection)) {
                        cmd.Parameters.AddWithValue("N", folder);
                        cmd.ExecuteNonQuery();
                    }
                }
            } catch { }
        }

        private static void SeedInitialAdmin(NpgsqlConnection connection)
        {
            // Seed Admin
            string checkAdminQuery = "SELECT COUNT(*) FROM Users WHERE Username = 'admin'";
            using (var checkCommand = new NpgsqlCommand(checkAdminQuery, connection))
            {
                long count = Convert.ToInt64(checkCommand.ExecuteScalar());
                if (count == 0)
                {
                    string hashedPassword = AuthService.HashPassword("admin123");
                    string insertAdminQuery = @"
                        INSERT INTO Users (Username, PasswordHash, Role, FullName, Email, Department, Designation) 
                        VALUES ('admin', @Password, 'Administrator', 'System Admin', 'admin@company.com', 'IT', 'System Administrator')";
                    using (var insertCommand = new NpgsqlCommand(insertAdminQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@Password", hashedPassword);
                        insertCommand.ExecuteNonQuery();
                    }
                }
            }

            // Seed Users requested by the user
            string[] names = {
                "Angeeswari", "Anusuya", "Barathi", "Deepa.T", "Dharani", "Gayathri", 
                "Hema", "Jeevitha", "Jeyanandhini", "Kaleeswari", "Karthika", "Mathavi", 
                "Nithiya", "Pandiselvi", "Reshma", "Sujitha", "Swathi", "Varshini", 
                "Lakshmipriya", "Priyadharshini.K"
            };

            string userPasswordHash = AuthService.HashPassword("vendhan@123");

            foreach (var name in names)
            {
                string username = name.Replace(".", "").Replace(" ", "").ToLower();
                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM Users WHERE Username = @u", connection))
                {
                    cmd.Parameters.AddWithValue("u", username);
                    if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
                    {
                        string sql = "INSERT INTO Users (Username, PasswordHash, Role, FullName, IsActive) VALUES (@u, @p, 'Employee', @f, 1)";
                        using (var ins = new NpgsqlCommand(sql, connection))
                        {
                            ins.Parameters.AddWithValue("u", username);
                            ins.Parameters.AddWithValue("p", userPasswordHash);
                            ins.Parameters.AddWithValue("f", name);
                            ins.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        public static void ClearAllData()
        {
            // Dropping tables completely would clear postgres data, a bit riskier in production but ok for reset
            using (var connection = GetConnection())
            {
                string dropQuery = @"
                    DROP SCHEMA public CASCADE;
                    CREATE SCHEMA public;
                ";
                using (var cmd = new NpgsqlCommand(dropQuery, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            InitializeDatabase();
        }
    }
}
