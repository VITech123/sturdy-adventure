using System;

namespace EnterpriseWorkReport.Models
{
    public class QualityReport
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string EmployeeName { get; set; }
        public DateTime ReportDate { get; set; }
        public double Accuracy { get; set; }
        public double ErrorRate { get; set; }
        public int ReworkCount { get; set; }
        public double QualityScore { get; set; }
        public string Remarks { get; set; }

        // Set by the page after loading threshold from DB; used for row highlighting
        public bool IsBelowThreshold { get; set; }
    }
}
