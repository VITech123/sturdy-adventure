using System;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Services
{
    public class CloudSyncService
    {
        private static CloudSyncService _instance;
        public static CloudSyncService Instance => _instance ??= new CloudSyncService();

        public event Action<string> SyncLog;
        public event Action<bool, string> SyncCompleted; // success, message

        private readonly string _localConnStr = DatabaseService.ConnectionString;
        private string _cloudConnStr;

        private CloudSyncService() { }

        public void ConfigureCloud(string host, string port, string dbname, string username, string password)
        {
            _cloudConnStr = $"Host={host};Port={port};Database={dbname};Username={username};Password={password};Pooling=true;Maximum Pool Size=50;";
        }

        public async Task<CloudSyncResult> SyncToCloudAsync()
        {
            if (string.IsNullOrEmpty(_cloudConnStr))
                return CloudSyncResult.Failure("Cloud database not configured. Please set credentials in Settings.");

            try
            {
                SyncLog?.Invoke("Starting cloud sync...");

                // Test cloud connection first
                using (var cloudConn = new NpgsqlConnection(_cloudConnStr))
                {
                    await cloudConn.OpenAsync();
                }

                // Get all data from local
                using (var localConn = DatabaseService.GetConnection())
                {
                    // Ensure cloud schema exists
                    await EnsureCloudSchemaAsync();

                    // Sync in transaction per table
                    int totalSynced = 0;

                    // 1. Users
                    var users = localConn.Query<User>("SELECT * FROM Users").ToList();
                    await BulkUpsertAsync(users, "Users", "Id");
                    totalSynced += users.Count;

                    // 2. Projects
                    var projects = localConn.Query<Project>("SELECT * FROM Projects").ToList();
                    await BulkUpsertAsync(projects, "Projects", "Id");
                    totalSynced += projects.Count;

                    // 3. ProjectFields
                    var fields = localConn.Query<ProjectField>("SELECT * FROM ProjectFields").ToList();
                    await BulkUpsertAsync(fields, "ProjectFields", "Id");
                    totalSynced += fields.Count;

                    // 4. WorkReports & Items
                    var workReports = localConn.Query<WorkReport>("SELECT * FROM WorkReports").ToList();
                    await BulkUpsertAsync(workReports, "WorkReports", "Id");
                    totalSynced += workReports.Count;
                    
                    var workReportItems = localConn.Query<WorkReportItem>("SELECT * FROM WorkReportItems").ToList();
                    await BulkUpsertAsync(workReportItems, "WorkReportItems", "Id");
                    totalSynced += workReportItems.Count;

                    // 5. Attendance
                    var attendance = localConn.Query<Attendance>("SELECT * FROM Attendance").ToList();
                    await BulkUpsertAsync(attendance, "Attendance", "Id");
                    totalSynced += attendance.Count;

                    // 6. Leaves
                    var leaves = localConn.Query<LeaveRequest>("SELECT * FROM Leaves").ToList();
                    await BulkUpsertAsync(leaves, "Leaves", "Id");
                    totalSynced += leaves.Count;

                    // 7. QualityReports & Items
                    var qualityReports = localConn.Query<QualityReport>("SELECT * FROM QualityReports").ToList();
                    await BulkUpsertAsync(qualityReports, "QualityReports", "Id");
                    totalSynced += qualityReports.Count;

                    var qualityItems = localConn.Query<QualityReportItem>("SELECT * FROM QualityReportItems").ToList();
                    await BulkUpsertAsync(qualityItems, "QualityReportItems", "Id");
                    totalSynced += qualityItems.Count;

                    // 8. AuditLogs
                    var auditLogs = localConn.Query<AuditLog>("SELECT * FROM AuditLogs").ToList();
                    await BulkUpsertAsync(auditLogs, "AuditLogs", "Id");
                    totalSynced += auditLogs.Count;

                    // 9. Messages
                    var messages = localConn.Query<Message>("SELECT * FROM Messages").ToList();
                    await BulkUpsertAsync(messages, "Messages", "Id");
                    totalSynced += messages.Count;

                    // 10. MasterData
                    var masterData = localConn.Query<MasterData>("SELECT * FROM MasterData").ToList();
                    await BulkUpsertAsync(masterData, "MasterData", "Id");
                    totalSynced += masterData.Count;

                    // 11. CompanySettings (upsert single row)
                    var companySettings = localConn.Query<CompanySettings>("SELECT * FROM CompanySettings WHERE Id = 1").FirstOrDefault();
                    if (companySettings != null)
                    {
                        await UpsertAsync(companySettings, "CompanySettings", "Id");
                        totalSynced++;
                    }

                    // 12. UserSettings
                    var userSettings = localConn.Query<UserSetting>("SELECT * FROM UserSettings").ToList();
                    await BulkUpsertAsync(userSettings, "UserSettings", new[] { "UserId", "Key" });
                    totalSynced += userSettings.Count;

                    // 13. Resumes & ResumeFolders
                    var resumeFolders = localConn.Query<ResumeFolder>("SELECT * FROM ResumeFolders").ToList();
                    await BulkUpsertAsync(resumeFolders, "ResumeFolders", "Id");
                    totalSynced += resumeFolders.Count;

                    var resumes = localConn.Query<Resume>("SELECT * FROM Resumes").ToList();
                    await BulkUpsertAsync(resumes, "Resumes", "Id");
                    totalSynced += resumes.Count;

                    SyncLog?.Invoke($"Cloud sync completed. {totalSynced} records synced.");
                    return CloudSyncResult.Success($"Successfully synced {totalSynced} records to cloud.");
                }
            }
            catch (Exception ex)
            {
                SyncLog?.Invoke($"Cloud sync failed: {ex.Message}");
                return CloudSyncResult.Failure($"Sync failed: {ex.Message}");
            }
        }

        private async Task EnsureCloudSchemaAsync()
        {
            var createTables = new[]
            {
                @"CREATE TABLE IF NOT EXISTS Users (
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
                );",
                @"CREATE TABLE IF NOT EXISTS Projects (
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
                );",
                @"CREATE TABLE IF NOT EXISTS ProjectFields (
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
                );",
                @"CREATE TABLE IF NOT EXISTS WorkReports (
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
                );",
                @"CREATE TABLE IF NOT EXISTS WorkReportItems (
                    Id SERIAL PRIMARY KEY,
                    WorkReportId INTEGER NOT NULL,
                    FieldId INTEGER NOT NULL,
                    Value TEXT,
                    FOREIGN KEY(WorkReportId) REFERENCES WorkReports(Id),
                    FOREIGN KEY(FieldId) REFERENCES ProjectFields(Id)
                );",
                @"CREATE TABLE IF NOT EXISTS Attendance (
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
                );",
                @"CREATE TABLE IF NOT EXISTS Leaves (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER NOT NULL,
                    LeaveType TEXT NOT NULL,
                    StartDate DATE NOT NULL,
                    EndDate DATE NOT NULL,
                    Reason TEXT,
                    Status TEXT DEFAULT 'Pending',
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );",
                @"CREATE TABLE IF NOT EXISTS QualityReports (
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
                );",
                @"CREATE TABLE IF NOT EXISTS QualityReportItems (
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
                );",
                @"CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER,
                    Username TEXT,
                    Action TEXT NOT NULL,
                    Details TEXT,
                    Timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );",
                @"CREATE TABLE IF NOT EXISTS Messages (
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
                );",
                @"CREATE TABLE IF NOT EXISTS MasterData (
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
                );",
                @"CREATE TABLE IF NOT EXISTS CompanySettings (
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
                    LateArrivalThreshold TEXT DEFAULT '09:30',
                    EarlyDepartureThreshold TEXT DEFAULT '18:00',
                    WorkStartTime TEXT DEFAULT '09:00',
                    WorkEndTime TEXT DEFAULT '18:00',
                    CloudDbHost TEXT,
                    CloudDbPort TEXT DEFAULT '5432',
                    CloudDbName TEXT,
                    CloudDbUsername TEXT,
                    CloudDbPasswordEncrypted TEXT,
                    CloudSyncEnabled BOOLEAN DEFAULT FALSE,
                    LastCloudSyncAt TIMESTAMP,
                    LastCloudSyncStatus TEXT,
                    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );",
                @"CREATE TABLE IF NOT EXISTS UserSettings (
                    UserId INTEGER NOT NULL,
                    Key TEXT NOT NULL,
                    Value TEXT,
                    PRIMARY KEY(UserId, Key),
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );",
                @"CREATE TABLE IF NOT EXISTS ResumeFolders (
                    Id SERIAL PRIMARY KEY,
                    Name TEXT UNIQUE NOT NULL,
                    Icon TEXT
                );",
                @"CREATE TABLE IF NOT EXISTS Resumes (
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
                );"
            };

            using (var cloudConn = new NpgsqlConnection(_cloudConnStr))
            {
                await cloudConn.OpenAsync();
                foreach (var sql in createTables)
                {
                    try { await cloudConn.ExecuteAsync(sql); } catch { }
                }
            }
        }

        private async Task BulkUpsertAsync<T>(System.Collections.Generic.IEnumerable<T> items, string tableName, string keyColumn)
        {
            if (!items.Any()) return;
            var itemList = items.ToList();
            if (!itemList.Any()) return;

            using var cloudConn = new NpgsqlConnection(_cloudConnStr);
            await cloudConn.OpenAsync();

            foreach (var item in itemList)
            {
                await UpsertAsync(item, tableName, keyColumn, cloudConn);
            }
        }

        private async Task BulkUpsertAsync<T>(System.Collections.Generic.IEnumerable<T> items, string tableName, string[] keyColumns)
        {
            if (!items.Any()) return;
            var itemList = items.ToList();
            if (!itemList.Any()) return;

            using var cloudConn = new NpgsqlConnection(_cloudConnStr);
            await cloudConn.OpenAsync();

            foreach (var item in itemList)
            {
                await UpsertAsync(item, tableName, keyColumns, cloudConn);
            }
        }

        private async Task UpsertAsync<T>(T item, string tableName, string keyColumn, NpgsqlConnection cloudConn = null)
        {
            bool closeConn = false;
            if (cloudConn == null)
            {
                cloudConn = new NpgsqlConnection(_cloudConnStr);
                await cloudConn.OpenAsync();
                closeConn = true;
            }

            try
            {
                var props = typeof(T).GetProperties();
                var keyProp = props.FirstOrDefault(p => p.Name.Equals(keyColumn, StringComparison.OrdinalIgnoreCase));
                if (keyProp == null) return;

                var keyValue = keyProp.GetValue(item);
                var cols = props.Where(p => p.Name != keyColumn).ToList();

                // Check if exists
                string checkSql = $"SELECT COUNT(*) FROM {tableName} WHERE \"{keyColumn}\" = @Key";
                var parameters = new { Key = keyValue };
                bool exists = await cloudConn.ExecuteScalarAsync<int>(checkSql, parameters) > 0;

                if (exists)
                {
                    // UPDATE
                    var setClause = string.Join(", ", cols.Select((c, i) => $"\"{c.Name}\" = @V{i}"));
                    string sql = $"UPDATE {tableName} SET {setClause} WHERE \"{keyColumn}\" = @Key";
                    var updateParams = new DynamicParameters();
                    updateParams.Add("Key", keyValue);
                    for (int i = 0; i < cols.Count; i++)
                    {
                        updateParams.Add($"V{i}", cols[i].GetValue(item));
                    }
                    await cloudConn.ExecuteAsync(sql, updateParams);
                }
                else
                {
                    // INSERT
                    var allCols = props.ToList();
                    var colNames = string.Join("\", \"", allCols.Select(p => p.Name));
                    var paramNames = string.Join(", ", allCols.Select(p => "@" + p.Name));
                    var insertParams = new DynamicParameters();
                    foreach (var prop in props)
                    {
                        insertParams.Add(prop.Name, prop.GetValue(item));
                    }
                    string sql = $"INSERT INTO {tableName} (\"{colNames}\") VALUES ({paramNames})";
                    await cloudConn.ExecuteAsync(sql, insertParams);
                }
            }
            finally
            {
                if (closeConn) cloudConn?.Dispose();
            }
        }

        // Overload for composite keys
        private async Task UpsertAsync<T>(T item, string tableName, string[] keyColumns, NpgsqlConnection cloudConn = null)
        {
            bool closeConn = false;
            if (cloudConn == null)
            {
                cloudConn = new NpgsqlConnection(_cloudConnStr);
                await cloudConn.OpenAsync();
                closeConn = true;
            }

            try
            {
                var props = typeof(T).GetProperties();
                var keyProps = props.Where(p => keyColumns.Any(k => k.Equals(p.Name, StringComparison.OrdinalIgnoreCase))).ToList();
                var cols = props.Except(keyProps).ToList();

                // Build WHERE clause for composite key
                var whereClause = string.Join(" AND ", keyProps.Select((kp, i) => $"\"{kp.Name}\" = @K{i}"));
                var checkSql = $"SELECT COUNT(*) FROM {tableName} WHERE {whereClause}";
                var checkParams = new DynamicParameters();
                for (int i = 0; i < keyProps.Count; i++)
                {
                    checkParams.Add($"K{i}", keyProps[i].GetValue(item));
                }

                bool exists = await cloudConn.ExecuteScalarAsync<int>(checkSql, checkParams) > 0;

                if (exists)
                {
                    var setClause = string.Join(", ", cols.Select((c, i) => $"\"{c.Name}\" = @V{i}"));
                    string sql = $"UPDATE {tableName} SET {setClause} WHERE {whereClause}";
                    var updateParams = new DynamicParameters();
                    for (int i = 0; i < cols.Count; i++) updateParams.Add($"V{i}", cols[i].GetValue(item));
                    for (int i = 0; i < keyProps.Count; i++) updateParams.Add($"K{i}", keyProps[i].GetValue(item));
                    await cloudConn.ExecuteAsync(sql, updateParams);
                }
                else
                {
                    var allCols = props.ToList();
                    var colNames = string.Join("\", \"", allCols.Select(p => p.Name));
                    var paramNames = string.Join(", ", allCols.Select(p => "@" + p.Name));
                    var insertParams = new DynamicParameters();
                    foreach (var prop in props) insertParams.Add(prop.Name, prop.GetValue(item));
                    string sql = $"INSERT INTO {tableName} (\"{colNames}\") VALUES ({paramNames})";
                    await cloudConn.ExecuteAsync(sql, insertParams);
                }
            }
            finally
            {
                if (closeConn) cloudConn?.Dispose();
            }
        }

        public async Task TestConnectionAsync()
        {
            using var conn = new NpgsqlConnection(_cloudConnStr);
            await conn.OpenAsync();
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_cloudConnStr);
    }

    public class CloudSyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int RecordsSynced { get; set; }

        public static CloudSyncResult Success(string msg, int count = 0) => new CloudSyncResult { Success = true, Message = msg, RecordsSynced = count };
        public static CloudSyncResult Failure(string msg) => new CloudSyncResult { Success = false, Message = msg };
    }
}
