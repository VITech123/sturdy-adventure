using System;
using System.Collections.Generic;

namespace EnterpriseWorkReport.Models
{
    public class DailyStatistic
    {
        public int Id { get; set; }
        public DateTime StatDate { get; set; }  // The date (from EndDate/batch)
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        
        // Manifest counts
        public int TotalManifests { get; set; }
        public int DistinctManifests { get; set; }
        
        // Object counts
        public int TotalObjects { get; set; }
        public int FinishedCount { get; set; }
        public int ShippedCount { get; set; }
        public int PendingCount { get; set; }
        public int ErrorCount { get; set; }
        public int HoldCount { get; set; }
        public int UnassignedCount { get; set; }
        
        // Totals
        public int TotalPages { get; set; }
        public int TotalArticles { get; set; }
        public int TotalCharacters { get; set; }
        
        // Work reports for this date
        public int WorkReportsCount { get; set; }
        public decimal WorkReportsBilling { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
