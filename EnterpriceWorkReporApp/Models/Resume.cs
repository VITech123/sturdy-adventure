using System;

namespace EnterpriseWorkReport.Models
{
    public class Resume
    {
        public int Id { get; set; }
        public int? FolderId { get; set; }
        public string CandidateName { get; set; }
        public string ContactNumber { get; set; }
        public string Email { get; set; }
        public string Experience { get; set; }
        public string ImagePath { get; set; }
        public string ThumbnailPath { get; set; }
        public string Status { get; set; } // Ongoing, Pending, Rejected, Relieved
        public string ExtractedText { get; set; }
        public DateTime UploadDate { get; set; }
    }
}
