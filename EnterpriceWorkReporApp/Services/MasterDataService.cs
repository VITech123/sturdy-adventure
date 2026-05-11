using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using ExcelDataReader;
using EnterpriseWorkReport.Models;
using Newtonsoft.Json;

namespace EnterpriseWorkReport.Services
{
    public class MasterDataService
    {
        public List<MasterData> GetAll(int? projectId = null, string status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var conn = DatabaseService.GetConnection();
            string where = "WHERE 1=1";
            if (projectId.HasValue) where += " AND m.ProjectId = @ProjectId";
            if (!string.IsNullOrEmpty(status)) where += " AND m.Status = @Status";
            if (fromDate.HasValue) where += " AND COALESCE(m.StartDate, m.ImportDate)::DATE >= @FromDate::DATE";
            if (toDate.HasValue) where += " AND COALESCE(m.StartDate, m.ImportDate)::DATE <= @ToDate::DATE";

            return conn.Query<MasterData>(@$"
                SELECT m.*, p.Name AS ProjectName, u.FullName AS AssignedUserName
                FROM MasterData m
                JOIN Projects p ON p.Id = m.ProjectId
                LEFT JOIN Users u ON u.Id = m.AssignedUserId
                {where}
                ORDER BY m.ImportDate DESC, m.Id DESC", new { ProjectId = projectId, Status = status, FromDate = fromDate, ToDate = toDate }).ToList();
        }

        public MasterData GetById(int id)
        {
            using var conn = DatabaseService.GetConnection();
            return conn.QueryFirstOrDefault<MasterData>(@"
                SELECT m.*, p.Name AS ProjectName, u.FullName AS AssignedUserName
                FROM MasterData m
                JOIN Projects p ON p.Id = m.ProjectId
                LEFT JOIN Users u ON u.Id = m.AssignedUserId
                WHERE m.Id = @Id", new { Id = id });
        }

        public MasterData GetByObjectId(string objectId)
        {
            using var conn = DatabaseService.GetConnection();
            return conn.QueryFirstOrDefault<MasterData>(@"
                SELECT m.*, p.Name AS ProjectName, u.FullName AS AssignedUserName
                FROM MasterData m
                JOIN Projects p ON p.Id = m.ProjectId
                LEFT JOIN Users u ON u.Id = m.AssignedUserId
                WHERE m.ObjectId = @ObjectId", new { ObjectId = objectId });
        }

        public List<MasterData> GetUnassigned(int projectId)
        {
            using var conn = DatabaseService.GetConnection();
            return conn.Query<MasterData>(@"
                SELECT m.*, p.Name AS ProjectName
                FROM MasterData m
                JOIN Projects p ON p.Id = m.ProjectId
                WHERE m.ProjectId = @ProjectId AND (m.Status = 'Unassigned' OR m.Status IS NULL)
                ORDER BY m.StartDate DESC", new { ProjectId = projectId }).ToList();
        }

        public List<MasterData> GetErrors()
        {
            using var conn = DatabaseService.GetConnection();
            return conn.Query<MasterData>(@"
                SELECT m.*, p.Name AS ProjectName, u.FullName AS AssignedUserName
                FROM MasterData m
                JOIN Projects p ON p.Id = m.ProjectId
                LEFT JOIN Users u ON u.Id = m.AssignedUserId
                WHERE m.Status = 'Error'
                ORDER BY m.EndDate DESC").ToList();
        }

        public MasterDataSummary GetSummary(int? projectId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var conn = DatabaseService.GetConnection();
            string where = "WHERE 1=1";
            if (projectId.HasValue) where += " AND ProjectId = @ProjectId";
            if (fromDate.HasValue) where += " AND COALESCE(StartDate, ImportDate)::DATE >= @FromDate::DATE";
            if (toDate.HasValue) where += " AND COALESCE(StartDate, ImportDate)::DATE <= @ToDate::DATE";

            var result = conn.QueryFirstOrDefault<MasterDataSummary>(@$"
                SELECT 
                    COUNT(DISTINCT ManifestId) AS TotalManifests,
                    COUNT(*) AS TotalObjects,
                    COALESCE(SUM(Pages), 0) AS TotalPages,
                    COALESCE(SUM(Articles), 0) AS TotalArticles,
                    COALESCE(SUM(CharacterCount), 0) AS TotalCharacters,
                    SUM(CASE WHEN Status = 'Shipped' THEN 1 ELSE 0 END) AS ShippedCount,
                    SUM(CASE WHEN Status = 'Unassigned' OR Status IS NULL THEN 1 ELSE 0 END) AS UnassignedCount,
                    SUM(CASE WHEN Status = 'Hold' THEN 1 ELSE 0 END) AS HoldCount,
                    SUM(CASE WHEN Status = 'Error' THEN 1 ELSE 0 END) AS ErrorCount
                FROM MasterData
                {where}", new { ProjectId = projectId, FromDate = fromDate, ToDate = toDate });

            return result ?? new MasterDataSummary();
        }

        public List<MasterDataStatusSummary> GetStatusBreakdown(int? projectId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var conn = DatabaseService.GetConnection();
            string where = "WHERE 1=1";
            if (projectId.HasValue) where += " AND ProjectId = @ProjectId";
            if (fromDate.HasValue) where += " AND COALESCE(StartDate, ImportDate)::DATE >= @FromDate::DATE";
            if (toDate.HasValue) where += " AND COALESCE(StartDate, ImportDate)::DATE <= @ToDate::DATE";

            return conn.Query<MasterDataStatusSummary>(@$"
                SELECT 
                    COALESCE(Status, 'Unassigned') AS Status,
                    COUNT(*) AS Count,
                    SUM(Pages) AS TotalPages,
                    SUM(Articles) AS TotalArticles,
                    SUM(CharacterCount) AS TotalCharacters
                FROM MasterData
                {where}
                GROUP BY Status
                ORDER BY Count DESC", new { ProjectId = projectId, FromDate = fromDate, ToDate = toDate }).ToList();
        }

        public int SyncMasterFile(int projectId, string filePath, bool replaceExisting = false, string password = null, FuzzyNameMatcher nameMatcher = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Master file not found: " + filePath);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            DataTable dt;

            if (ext == ".csv")
            {
                dt = ReadCsvFile(filePath);
            }
            else if (ext == ".xlsx" || ext == ".xls")
            {
                dt = ReadExcelFile(filePath, password);
            }
            else
            {
                throw new NotSupportedException("Unsupported file format: " + ext);
            }

            int imported = 0;
            string[] requiredColumns = { "Object_ID", "ObjectId", "Object ID", "object_Id" };
            bool hasObjectId = false;
            foreach (DataColumn col in dt.Columns)
            {
                if (requiredColumns.Any(c => c.Equals(col.ColumnName, StringComparison.OrdinalIgnoreCase)))
                {
                    hasObjectId = true;
                    break;
                }
            }

            if (!hasObjectId)
                throw new Exception("Required column 'Object_ID' not found in master file");

            using var conn = DatabaseService.GetConnection();
            using var transaction = conn.BeginTransaction();

            try
            {
                foreach (DataRow row in dt.Rows)
                {
                    string objectId = GetColumnValue(row, "Object_ID", "ObjectId", "Object ID", "object_Id");
                    if (string.IsNullOrWhiteSpace(objectId)) continue;

                     string manifestId = GetColumnValue(row, "Manifest_ID", "ManifestId", "Manifest ID", "Manifest", "manifest_id");
                     string objectName = GetColumnValue(row, "Name", "ObjectName", "Object Name", "object_name");
                     int pages = GetIntValue(row, "Pages", "Page", "PAGES", "PAGE", "No_of_Pages", "pages", "page_count", "Page_Count", "page", "pg", "PG", "Pg", "PageNo", "Page Number");
                     int articles = GetIntValue(row, "Articles", "Article_Count", "articles", "article_count", "ARTICLE_COUNT", "article", "ARTICLES", "artcount");
                     int characters = GetIntValue(row, "Charactercount", "Character_Count", "Characters", "CharacterCount", "character_count", "CHARACTER_COUNT", "charcount", "chars", "CHARACTERS", "CharacterCount", "No_of_Chars", "CharCount");
                    
                    string rawBatch = GetColumnValue(row, "Batch", "Batch_No", "BatchNumber", "batch_no", "batch");
                    string status = GetColumnValue(row, "Status", "status");
                    double quality = GetDoubleValue(row, "Quality", "QualityScore", "Quality_Score", "quality");
                    string errorDetails = GetColumnValue(row, "Error", "Error_Details", "ErrorDetails", "error");

                    DateTime? startDate = GetDateValue(row, "Start_Date", "StartDate", "Start Date", "start_date");
                    DateTime? endDate = GetDateValue(row, "End_Date", "EndDate", "End Date", "end_date");
                    string assignedUser = GetColumnValue(row, "NAME", "Assigned_To", "AssignedTo", "AssignedToName", "User", "Assigned Name", "assigned_to");

                    // 1. Shipment Date Extraction from Batch (e.g., Gamma143 - Shipment1 - 20260307)
                    // IMPORTANT: Date should ALWAYS come from Batch column for work report dating, NOT from Date column
                    DateTime? shipmentDate = null;
                    string rawBatch = GetColumnValue(row, "Batch", "Batch_No", "BatchNumber", "batch_no", "batch");
                    
                    if (!string.IsNullOrEmpty(rawBatch))
                    {
                        var match = Regex.Match(rawBatch, @"(\d{8})"); // Look for yyyyMMdd pattern
                        if (match.Success && DateTime.TryParseExact(match.Value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d))
                        {
                            shipmentDate = d;
                        }
                    }
                    
                    // Fallback: if no 8-digit in batch, try 6-digit format (yymmdd) or other patterns
                    if (!shipmentDate.HasValue && !string.IsNullOrEmpty(rawBatch))
                    {
                        var match6 = Regex.Match(rawBatch, @"(\d{6})"); // yymmdd pattern
                        if (match6.Success && match6.Value.Length == 6)
                        {
                            string year = "20" + match6.Value.Substring(0, 2); // Assume 20xx
                            string month = match6.Value.Substring(2, 2);
                            string day = match6.Value.Substring(4, 2);
                            if (DateTime.TryParseExact($"{year}{month}{day}", "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d2))
                            {
                                shipmentDate = d2;
                            }
                        }
                    }
                    
                    // If still no date from batch, that's an error - we need the date for work reports
                    if (!shipmentDate.HasValue)
                    {
                        // Log warning but continue - EndDate will be null/import date
                        System.Diagnostics.Debug.WriteLine($"Warning: Could not extract date from Batch column: '{rawBatch}'. Object ID: {GetColumnValue(row, "Object_ID", "ObjectId", "Object ID", "object_Id")}");
                    }
                    
                    // Use shipment date as EndDate (the date work should be reported for)
                    // Also use as StartDate if StartDate is not provided separately
                    DateTime? startDate = GetDateValue(row, "Start_Date", "StartDate", "Start Date", "start_date");
                    if (!startDate.HasValue) startDate = shipmentDate;
                    DateTime? endDate = shipmentDate; // Always prefer batch date

                    // 2. Status Resolution Logic based on User requirements
                    string resolvedStatus = "Unassigned";
                    if (!string.IsNullOrEmpty(assignedUser))
                    {
                        resolvedStatus = "Pending"; // Assigned but not necessarily finished
                        
                        if (!string.IsNullOrEmpty(rawBatch) && (rawBatch.ToLower().Contains("ship") || shipmentDate.HasValue))
                        {
                            resolvedStatus = "Shipped";
                        }
                        else if (!string.IsNullOrEmpty(errorDetails) && errorDetails.ToLower() != "none" && errorDetails.ToLower() != "0")
                        {
                            resolvedStatus = "Error";
                        }
                        else if (status.ToLower().Contains("hold"))
                        {
                            resolvedStatus = "Hold";
                        }
                    }
                    
                    // If status says error and batch is empty, it's Error (not finished)
                    if (status.ToLower().Contains("error") && string.IsNullOrEmpty(rawBatch))
                    {
                        resolvedStatus = "Error";
                    }

                    // 3. Store all columns in ExtraData for "Display as is"
                    var extraDataDict = new Dictionary<string, string>();
                    foreach (DataColumn col in row.Table.Columns)
                    {
                        extraDataDict[col.ColumnName] = row[col]?.ToString() ?? "";
                    }
                    string extraDataJson = JsonConvert.SerializeObject(extraDataDict);

                    // Fuzzy-match the assigned user name against master names
                    if (!string.IsNullOrWhiteSpace(assignedUser) && nameMatcher != null && nameMatcher.IsLoaded)
                        assignedUser = nameMatcher.Resolve(assignedUser);

                    conn.Execute(@"
                        INSERT INTO MasterData 
                        (ProjectId, ManifestId, ObjectId, ObjectName, Pages, Articles, CharacterCount, StartDate, EndDate, Status, Batch, QualityScore, AssignedUserName, ErrorDetails, ExtraData, ImportDate)
                        VALUES 
                        (@ProjectId, @ManifestId, @ObjectId, @ObjectName, @Pages, @Articles, @CharacterCount, @StartDate, @EndDate, @Status, @Batch, @QualityScore, @AssignedUserName, @ErrorDetails, @ExtraData, @ImportDate)
                        ON CONFLICT (ProjectId, ObjectId) DO UPDATE SET
                        ManifestId = EXCLUDED.ManifestId,
                        ObjectName = EXCLUDED.ObjectName,
                        Pages = EXCLUDED.Pages,
                        Articles = EXCLUDED.Articles,
                        CharacterCount = EXCLUDED.CharacterCount,
                        Status = EXCLUDED.Status,
                        Batch = EXCLUDED.Batch,
                        QualityScore = EXCLUDED.QualityScore,
                        AssignedUserName = EXCLUDED.AssignedUserName,
                        ErrorDetails = EXCLUDED.ErrorDetails,
                        ExtraData = EXCLUDED.ExtraData,
                        ImportDate = EXCLUDED.ImportDate",
                        new 
                        {
                            ProjectId = projectId,
                            ManifestId = manifestId,
                            ObjectId = objectId,
                            ObjectName = objectName,
                            Pages = pages,
                            Articles = articles,
                            CharacterCount = characters,
                            StartDate = startDate,
                            EndDate = endDate,
                            Status = resolvedStatus,
                            Batch = rawBatch,
                            QualityScore = quality,
                            AssignedUserName = assignedUser,
                            ErrorDetails = errorDetails,
                            ExtraData = extraDataJson,
                            ImportDate = DateTime.Now
                        }, transaction);

                    imported++;
                }

                transaction.Commit();
                AuditService.Log("Master File Sync", $"Imported {imported} records for project {projectId} from {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception("Import failed: " + ex.Message, ex);
            }

            return imported;
        }

        public void AssignToUser(int masterDataId, int userId, string userName)
        {
            using var conn = DatabaseService.GetConnection();
            var masterData = conn.QueryFirstOrDefault<MasterData>("SELECT * FROM MasterData WHERE Id = @Id", new { Id = masterDataId });
            if (masterData == null) return;

            conn.Execute(@"
                UPDATE MasterData 
                SET AssignedUserId = @UserId, AssignedUserName = @UserName, Status = 'Assigned'
                WHERE Id = @Id",
                new { Id = masterDataId, UserId = userId, UserName = userName });

            // Send notification to the assigned user
            string message = $"You have been assigned a new object: {masterData.ObjectId} from manifest {masterData.ManifestId}. Pages: {masterData.Pages}.";
            conn.Execute(@"
                INSERT INTO Messages (SenderId, ReceiverId, Subject, Body, IsRead, SentDate)
                VALUES (@SenderId, @ReceiverId, @Subject, @Body, 0, @SentDate)",
                new { SenderId = SessionManager.CurrentUser?.Id ?? 0, ReceiverId = userId, Subject = "New Object Assignment", Body = message, SentDate = DateTime.Now });

            AuditService.Log("Object Assigned", $"Object ID {masterDataId} assigned to user {userName}");
        }

        public void UpdateStatus(int masterDataId, string status, string errorDetails = null)
        {
            using var conn = DatabaseService.GetConnection();
            conn.Execute(@"
                UPDATE MasterData 
                SET Status = @Status, ErrorDetails = @ErrorDetails
                WHERE Id = @Id",
                new { Id = masterDataId, Status = status, ErrorDetails = errorDetails });
            AuditService.Log("Status Updated", $"Object ID {masterDataId} status changed to {status}");
        }

        public void Delete(int id)
        {
            using var conn = DatabaseService.GetConnection();
            conn.Execute("DELETE FROM MasterData WHERE Id = @Id", new { Id = id });
            AuditService.Log("MasterData Deleted", $"Deleted record ID {id}");
        }

        public void DeleteByProject(int projectId)
        {
            using var conn = DatabaseService.GetConnection();
            conn.Execute("DELETE FROM MasterData WHERE ProjectId = @ProjectId", new { ProjectId = projectId });
            AuditService.Log("MasterData Cleared", $"Cleared all records for project {projectId}");
        }

        private DataTable ReadCsvFile(string path)
        {
            var dt = new DataTable();
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0) return dt;

            var headers = lines[0].Split(',');
            foreach (var header in headers)
            {
                dt.Columns.Add(header.Trim().Trim('"'));
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCsvLine(lines[i]);
                if (values.Length > 0)
                {
                    var row = dt.NewRow();
                    for (int j = 0; j < Math.Min(values.Length, dt.Columns.Count); j++)
                    {
                        row[j] = values[j].Trim().Trim('"');
                    }
                    dt.Rows.Add(row);
                }
            }

            return dt;
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = "";

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            result.Add(current);

            return result.ToArray();
        }

        private DataTable ReadExcelFile(string path, string password = null)
        {
            try
            {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var config = new ExcelReaderConfiguration();
                    if (!string.IsNullOrEmpty(password))
                        config.Password = password;

                    using (var reader = ExcelReaderFactory.CreateReader(stream, config))
                    {
                        var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                        {
                            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                        });

                        if (ds.Tables.Count == 0)
                            throw new Exception("Excel file has no sheets.");

                        return ds.Tables[0];
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
                    || ex.Message.IndexOf("encrypt", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new Exception($"This Excel file is password-protected. Please provide the correct password. ({ex.Message})");
                throw new Exception("Failed to read Excel file: " + ex.Message, ex);
            }
        }

        private string GetColumnValue(DataRow row, params string[] columnNames)
        {
            foreach (var name in columnNames)
            {
                string normalizedSearch = name.Replace("_", "").Replace(" ", "").ToLower();
                foreach (DataColumn col in row.Table.Columns)
                {
                    string normalizedCol = col.ColumnName.Replace("_", "").Replace(" ", "").ToLower();
                    if (normalizedCol == normalizedSearch)
                    {
                        var val = row[col];
                        return val?.ToString() ?? "";
                    }
                }
            }
            return "";
        }

        private int GetIntValue(DataRow row, params string[] columnNames)
        {
            var val = GetColumnValue(row, columnNames);
            if (string.IsNullOrWhiteSpace(val)) return 0;
            
            // Clean the string: remove commas, spaces, currency symbols
            val = val.Trim().Replace(",", "").Replace(" ", "").Replace("₹", "").Replace("$", "").Replace("€", "");
            
            // Try parsing as double first to handle "123.0" or formatted strings
            if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dblResult))
                return (int)Math.Round(dblResult);
            
            // Try extracting just the numeric part if there's mixed content
            var numericMatch = System.Text.RegularExpressions.Regex.Match(val, @"[\d,]+\.?\d*");
            if (numericMatch.Success)
            {
                string numStr = numericMatch.Value.Replace(",", "");
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dblResult2))
                    return (int)Math.Round(dblResult2);
            }
            
            return 0;
        }

        private double GetDoubleValue(DataRow row, params string[] columnNames)
        {
            var val = GetColumnValue(row, columnNames);
            if (string.IsNullOrWhiteSpace(val)) return 0;

            if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result))
                return result;
            return 0;
        }

        private DateTime? GetDateValue(DataRow row, params string[] columnNames)
        {
            var val = GetColumnValue(row, columnNames);
            if (DateTime.TryParse(val, out DateTime result))
                return result;
            return null;
        }

        public List<ManifestDetail> GetManifestDetails(int? projectId = null, DateTime? date = null, bool filterByEndDate = true)
        {
            using var conn = DatabaseService.GetConnection();
            string where = "WHERE 1=1";
            if (projectId.HasValue) where += " AND ProjectId = @ProjectId";
            if (date.HasValue)
            {
                if (filterByEndDate)
                    where += " AND DATE(EndDate) = @Date";
                else
                    where += " AND DATE(ImportDate) = @Date";
            }

            return conn.Query<ManifestDetail>(@$"
                SELECT 
                    ManifestId,
                    COUNT(*) AS ObjectCount,
                    SUM(Pages) AS TotalPages,
                    SUM(CASE WHEN Status = 'Finished' THEN 1 ELSE 0 END) AS FinishedCount,
                    SUM(CASE WHEN Status = 'Shipped' THEN 1 ELSE 0 END) AS ShippedCount,
                    SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END) AS PendingCount,
                    SUM(CASE WHEN Status = 'Error' THEN 1 ELSE 0 END) AS ErrorCount,
                    SUM(CASE WHEN Status = 'Hold' THEN 1 ELSE 0 END) AS HoldCount,
                    SUM(CASE WHEN Status = 'Unassigned' OR Status IS NULL THEN 1 ELSE 0 END) AS UnassignedCount
                FROM MasterData
                {where}
                GROUP BY ManifestId
                ORDER BY ManifestId", new { ProjectId = projectId, Date = date }).ToList();
        }

        public int GetTodayManifestCount(int? projectId = null)
        {
            using var conn = DatabaseService.GetConnection();
            string where = projectId.HasValue ? "WHERE ProjectId = @ProjectId AND DATE(EndDate) = CURRENT_DATE" : "WHERE DATE(EndDate) = CURRENT_DATE";
            return conn.ExecuteScalar<int>($"SELECT COUNT(DISTINCT ManifestId) FROM MasterData {where}", new { ProjectId = projectId });
        }

        public List<ManifestDetail> GetTodayManifestDetails(int? projectId = null)
        {
            using var conn = DatabaseService.GetConnection();
            string where = "WHERE DATE(COALESCE(EndDate, ImportDate)) = CURRENT_DATE";
            if (projectId.HasValue) where += " AND ProjectId = @ProjectId";

            return conn.Query<ManifestDetail>(@$"
                SELECT 
                    ManifestId,
                    COUNT(*) AS ObjectCount,
                    SUM(Pages) AS TotalPages,
                    SUM(CASE WHEN Status = 'Finished' THEN 1 ELSE 0 END) AS FinishedCount,
                    SUM(CASE WHEN Status = 'Shipped' THEN 1 ELSE 0 END) AS ShippedCount,
                    SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END) AS PendingCount,
                    SUM(CASE WHEN Status = 'Error' THEN 1 ELSE 0 END) AS ErrorCount,
                    SUM(CASE WHEN Status = 'Hold' THEN 1 ELSE 0 END) AS HoldCount,
                    SUM(CASE WHEN Status = 'Unassigned' OR Status IS NULL THEN 1 ELSE 0 END) AS UnassignedCount
                FROM MasterData
                {where}
                GROUP BY ManifestId
                ORDER BY ManifestId", new { ProjectId = projectId }).ToList();
        }
    }

    public class ManifestDetail
    {
        public string ManifestId { get; set; }
        public int ObjectCount { get; set; }
        public int TotalPages { get; set; }
        public int FinishedCount { get; set; }
        public int ShippedCount { get; set; }
        public int PendingCount { get; set; }
        public int ErrorCount { get; set; }
        public int HoldCount { get; set; }
        public int UnassignedCount { get; set; }
    }
}
