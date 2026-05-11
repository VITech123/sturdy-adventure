using System;

namespace EnterpriseWorkReport.Models
{
     public class CompanySettings
     {
         public int Id { get; set; }
         public string CompanyName { get; set; }
         public string CompanyAddress { get; set; }
         public string CompanyPhone { get; set; }
         public string CompanyEmail { get; set; }
         public string TaxId { get; set; }
         public string CurrencySymbol { get; set; }
         public string TimeZone { get; set; }
         public string LogoPath { get; set; }
         public string QualityReportsPath { get; set; } // Base path for quality report CSV files
         public string MasterNamesPath { get; set; } // Path to MasterNames.xlsx for fuzzy name matching
         public string AttachmentsPath { get; set; } // Network share path for attachments
         public string ProfilePicturesPath { get; set; } // Network share path for profile pictures
         public double QualityThreshold { get; set; } = 85; // Quality score below this is flagged
         public string LateArrivalThreshold { get; set; } = "09:30"; // Time after which clock-in is marked as late (HH:mm)
         public string EarlyDepartureThreshold { get; set; } = "18:00"; // Time before which clock-out is marked as early (HH:mm)
         public string WorkStartTime { get; set; } = "09:00"; // Standard work start time
         public string WorkEndTime { get; set; } = "18:00"; // Standard work end time
         
         // Cloud Database Sync Settings
         public string CloudDbHost { get; set; }
         public string CloudDbPort { get; set; } = "5432";
         public string CloudDbName { get; set; }
         public string CloudDbUsername { get; set; }
         public string CloudDbPasswordEncrypted { get; set; } // Encrypted via SecretService
         public bool CloudSyncEnabled { get; set; } = false;
         public DateTime? LastCloudSyncAt { get; set; }
         public string LastCloudSyncStatus { get; set; }
         
         public DateTime UpdatedAt { get; set; }

         // Helper to get decrypted cloud password
         public string CloudDbPassword => string.IsNullOrEmpty(CloudDbPasswordEncrypted) ? null : 
             Services.SecretService.DecryptSecret(CloudDbPasswordEncrypted);
         public void SetCloudDbPassword(string password) => CloudDbPasswordEncrypted = 
             string.IsNullOrEmpty(password) ? null : Services.SecretService.EncryptSecret(password);
     }
}
