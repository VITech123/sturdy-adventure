using System;
using System.Collections.Generic;
using System.Linq;

namespace EnterpriseWorkReport.Models
{
    public class WorkReport
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int UserId { get; set; }
        public string ObjectId { get; set; }
        public DateTime SubmissionDate { get; set; } = DateTime.Now;
        public decimal BillingAmount { get; set; }
        public string AdminNote { get; set; }
        public string AttachmentPath { get; set; }
        public string Status { get; set; } = "Submitted";
        public string ExtraFields { get; set; } // JSON column for additional data
        
        // Navigation / display helpers
        public string ProjectName { get; set; }
        public string EmployeeName { get; set; }
        public bool HasAttachment => !string.IsNullOrEmpty(AttachmentPath);
        public string AttachmentFileName => string.IsNullOrEmpty(AttachmentPath) ? "" : System.IO.Path.GetFileName(AttachmentPath);
        public List<WorkReportItem> Items { get; set; } = new List<WorkReportItem>();

        public int CharacterCount
        {
            get
            {
                var val = Items?.FirstOrDefault(i =>
                    !string.IsNullOrEmpty(i.FieldLabel) && (
                        i.FieldLabel.IndexOf("char", StringComparison.OrdinalIgnoreCase) >= 0
                    ))?.Value;
                
                if (string.IsNullOrWhiteSpace(val)) return 0;
                // Handle cases like "1,000" or decimal "10.0"
                if (double.TryParse(val.Replace(",", ""), out double count))
                    return (int)Math.Round(count);
                return 0;
            }
        }

        public int PagesCount
        {
            get
            {
                var val = Items?.FirstOrDefault(i =>
                    !string.IsNullOrEmpty(i.FieldLabel) && (
                        i.FieldLabel.IndexOf("page", StringComparison.OrdinalIgnoreCase) >= 0
                    ))?.Value;
                
                if (string.IsNullOrWhiteSpace(val)) return 0;
                if (double.TryParse(val.Replace(",", ""), out double count))
                    return (int)Math.Round(count);
                return 0;
            }
        }
    }

    public class WorkReportItem
    {
        public int Id { get; set; }
        public int WorkReportId { get; set; }
        public int FieldId { get; set; }
        public string FieldLabel { get; set; }
        public string Value { get; set; }
    }
}
