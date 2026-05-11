using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Dapper;
using EnterpriseWorkReport.Models;

namespace EnterpriseWorkReport.Services
{
    public class StatisticsService
    {
        public void SaveDailyStatistics(DateTime date, int? projectId = null)
        {
            using var conn = DatabaseService.GetConnection();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Get master data stats for the date (using EndDate = batch date)
                var masterStats = conn.Query(@$"
                    SELECT 
                        COUNT(DISTINCT ManifestId) as DistinctManifests,
                        COUNT(*) as TotalObjects,
                        SUM(Pages) as TotalPages,
                        SUM(Articles) as TotalArticles,
                        SUM(CharacterCount) as TotalCharacters,
                        SUM(CASE WHEN Status = 'Finished' THEN 1 ELSE 0 END) as FinishedCount,
                        SUM(CASE WHEN Status = 'Shipped' THEN 1 ELSE 0 END) as ShippedCount,
                        SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END) as PendingCount,
                        SUM(CASE WHEN Status = 'Error' THEN 1 ELSE 0 END) as ErrorCount,
                        SUM(CASE WHEN Status = 'Hold' THEN 1 ELSE 0 END) as HoldCount,
                        SUM(CASE WHEN Status = 'Unassigned' OR Status IS NULL THEN 1 ELSE 0 END) as UnassignedCount
                    FROM MasterData
                    WHERE DATE(COALESCE(EndDate, ImportDate)) = @Date
                    {(projectId.HasValue ? "AND ProjectId = @ProjectId" : "")}",
                    new { Date = date, ProjectId = projectId }).FirstOrDefault();

                // Get work reports for the date
                var workReportStats = conn.Query(@$"
                    SELECT 
                        COUNT(*) as WorkReportsCount,
                        SUM(BillingAmount) as WorkReportsBilling
                    FROM WorkReports
                    WHERE CAST(SubmissionDate AS DATE) = @Date
                    {(projectId.HasValue ? "AND ProjectId = @ProjectId" : "")}",
                    new { Date = date, ProjectId = projectId }).FirstOrDefault();

                // Build the project name
                string projectName = null;
                if (projectId.HasValue)
                {
                    projectName = conn.ExecuteScalar<string>("SELECT Name FROM Projects WHERE Id = @Id", new { Id = projectId.Value });
                }

                // Upsert into DailyStatistics
                string upsertSql = @"
                    INSERT INTO DailyStatistics (
                        StatDate, ProjectId, ProjectName,
                        TotalManifests, DistinctManifests,
                        TotalObjects, FinishedCount, ShippedCount, PendingCount, 
                        ErrorCount, HoldCount, UnassignedCount,
                        TotalPages, TotalArticles, TotalCharacters,
                        WorkReportsCount, WorkReportsBilling
                    ) VALUES (
                        @StatDate, @ProjectId, @ProjectName,
                        @DistinctManifests, @DistinctManifests,
                        @TotalObjects, @FinishedCount, @ShippedCount, @PendingCount,
                        @ErrorCount, @HoldCount, @UnassignedCount,
                        @TotalPages, @TotalArticles, @TotalCharacters,
                        @WorkReportsCount, @WorkReportsBilling
                    )
                    ON CONFLICT (StatDate, ProjectId) DO UPDATE SET
                        TotalManifests = EXCLUDED.TotalManifests,
                        DistinctManifests = EXCLUDED.DistinctManifests,
                        TotalObjects = EXCLUDED.TotalObjects,
                        FinishedCount = EXCLUDED.FinishedCount,
                        ShippedCount = EXCLUDED.ShippedCount,
                        PendingCount = EXCLUDED.PendingCount,
                        ErrorCount = EXCLUDED.ErrorCount,
                        HoldCount = EXCLUDED.HoldCount,
                        UnassignedCount = EXCLUDED.UnassignedCount,
                        TotalPages = EXCLUDED.TotalPages,
                        TotalArticles = EXCLUDED.TotalArticles,
                        TotalCharacters = EXCLUDED.TotalCharacters,
                        WorkReportsCount = EXCLUDED.WorkReportsCount,
                        WorkReportsBilling = EXCLUDED.WorkReportsBilling,
                        UpdatedAt = CURRENT_TIMESTAMP
                ";

                conn.Execute(upsertSql, new
                {
                    StatDate = date,
                    ProjectId = projectId,
                    ProjectName = projectName,
                    DistinctManifests = (int)masterStats.distinctmanifests,
                    TotalObjects = (int)masterStats.totalobjects,
                    TotalPages = (int?)masterStats.totalpages ?? 0,
                    TotalArticles = (int?)masterStats.totalarticles ?? 0,
                    TotalCharacters = (int?)masterStats.totalcharacters ?? 0,
                    FinishedCount = (int)masterStats.finishedcount,
                    ShippedCount = (int)masterStats.shippedcount,
                    PendingCount = (int)masterStats.pendingcount,
                    ErrorCount = (int)masterStats.errorcount,
                    HoldCount = (int)masterStats.holdcount,
                    UnassignedCount = (int)masterStats.unassignedcount,
                    WorkReportsCount = (int)workReportStats.workreportscount,
                    WorkReportsBilling = (decimal?)workReportStats.workreportsbilling ?? 0
                }, transaction);

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Error saving daily statistics: {ex.Message}");
                throw;
            }
        }

        public IEnumerable<DailyStatistic> GetStatistics(DateTime? fromDate = null, DateTime? toDate = null, int? projectId = null)
        {
            using var conn = DatabaseService.GetConnection();
            string where = "WHERE 1=1";
            if (fromDate.HasValue) where += " AND StatDate >= @FromDate";
            if (toDate.HasValue) where += " AND StatDate <= @ToDate";
            if (projectId.HasValue) where += " AND ProjectId = @ProjectId";

            return conn.Query<DailyStatistic>($@"
                SELECT * FROM DailyStatistics
                {where}
                ORDER BY StatDate DESC, ProjectId", 
                new { FromDate = fromDate, ToDate = toDate, ProjectId = projectId });
        }

        public DailyStatistic GetStatistic(DateTime date, int? projectId = null)
        {
            using var conn = DatabaseService.GetConnection();
            return conn.QueryFirstOrDefault<DailyStatistic>(
                "SELECT * FROM DailyStatistics WHERE StatDate = @Date AND (ProjectId = @ProjectId OR @ProjectId IS NULL)",
                new { Date = date, ProjectId = projectId });
        }
    }
}
