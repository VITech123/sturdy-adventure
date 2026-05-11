using Dapper;
using EnterpriseWorkReport.Models;

namespace EnterpriseWorkReport.Services
{
    public class CompanyService
    {
        public CompanySettings GetSettings()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                return conn.QueryFirstOrDefault<CompanySettings>("SELECT * FROM CompanySettings WHERE Id = 1");
            }
        }

        public void UpdateSettings(CompanySettings settings)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var sql = @"UPDATE CompanySettings SET 
                           CompanyName = @CompanyName,
                           CompanyAddress = @CompanyAddress,
                           CompanyPhone = @CompanyPhone,
                           CompanyEmail = @CompanyEmail,
                           TaxId = @TaxId,
                           CurrencySymbol = @CurrencySymbol,
                           TimeZone = @TimeZone,
                           LogoPath = @LogoPath,
                           MasterNamesPath = @MasterNamesPath,
                           QualityReportsPath = @QualityReportsPath,
                           AttachmentsPath = @AttachmentsPath,
                           ProfilePicturesPath = @ProfilePicturesPath,
                           QualityThreshold = @QualityThreshold,
                           LateArrivalThreshold = @LateArrivalThreshold,
                           EarlyDepartureThreshold = @EarlyDepartureThreshold,
                           WorkStartTime = @WorkStartTime,
                           WorkEndTime = @WorkEndTime,
                           CloudDbHost = @CloudDbHost,
                           CloudDbPort = @CloudDbPort,
                           CloudDbName = @CloudDbName,
                           CloudDbUsername = @CloudDbUsername,
                           CloudDbPasswordEncrypted = @CloudDbPasswordEncrypted,
                           CloudSyncEnabled = @CloudSyncEnabled,
                           LastCloudSyncAt = @LastCloudSyncAt,
                           LastCloudSyncStatus = @LastCloudSyncStatus,
                           UpdatedAt = CURRENT_TIMESTAMP
                           WHERE Id = 1";
                conn.Execute(sql, settings);
                AuditService.Log("Company Settings Updated", "Company settings were modified");
            }
        }
    }
}
