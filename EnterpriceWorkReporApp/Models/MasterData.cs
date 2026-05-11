using System;
using System.Collections.Generic;
using System.Linq;

namespace EnterpriseWorkReport.Models
{
    public class MasterData
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ManifestId { get; set; }
        public string ObjectId { get; set; }
        public string ObjectName { get; set; }
        public int Pages { get; set; }
        public int Articles { get; set; }
        public int CharacterCount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } // Shipped, Unassigned, Hold, Error
        public string Batch { get; set; }
        public double QualityScore { get; set; }
        public int? AssignedUserId { get; set; }
        public string AssignedUserName { get; set; }
        public string ErrorDetails { get; set; }
        public string ExtraData { get; set; }
        public DateTime? ImportDate { get; set; }

        public List<KeyValuePair<string, string>> ExtraDataItems
        {
            get
            {
                if (string.IsNullOrEmpty(ExtraData)) return new List<KeyValuePair<string, string>>();
                try
                {
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(ExtraData).ToList();
                }
                catch { return new List<KeyValuePair<string, string>>(); }
            }
        }

        public string ProjectName { get; set; }
        public string StatusDisplay => GetStatusDisplay();
        public bool IsError => Status?.Equals("Error", StringComparison.OrdinalIgnoreCase) == true;

        private string GetStatusDisplay()
        {
            if (string.IsNullOrEmpty(Status)) return "Unassigned";
            return Status switch
            {
                "Shipped" => "Shipped",
                "Unassigned" => "Unassigned",
                "Hold" => "On Hold",
                "Error" => "Error",
                _ => Status
            };
        }
    }

    public class MasterDataStatusSummary
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public int TotalPages { get; set; }
        public int TotalCharacters { get; set; }
    }

    public class MasterDataSummary
    {
        public int TotalManifests { get; set; }
        public int TotalObjects { get; set; }
        public int TotalPages { get; set; }
        public int TotalArticles { get; set; }
        public int TotalCharacters { get; set; }
        public int ShippedCount { get; set; }
        public int UnassignedCount { get; set; }
        public int HoldCount { get; set; }
        public int ErrorCount { get; set; }
    }
}