
using System.IO;

namespace EnterpriseWorkReport.Services
{
 public class BackupService
 {
  public void BackupDatabase()
  {
   File.Copy("Data/database.db","Backups/database_backup.db",true);
  }
 }
