using System;

namespace EnterpriseWorkReport.Models
{
    public class Project
    {
        public int Id { get; set; }
        public string ProjectCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public string BillingFormula { get; set; }
        public string MasterFilePath { get; set; }
        public string MasterFilePassword { get; set; }
        public string QualityBasePath { get; set; }
        public DateTime? LastMasterSyncAt { get; set; }
        public string LastMasterSyncStatus { get; set; }
    }
}
