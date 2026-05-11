using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using System.Collections.Generic;
using System.Data;
using ExcelDataReader;

namespace DBCreator
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string connectionString = "Host=localhost;Port=5432;Database=vendhan;Username=postgres;Password=vendhan@123;Pooling=true;Maximum Pool Size=100;";
                
                string[] names = {
                    "Angeeswari", "Anusuya", "Barathi", "Deepa.T", "Dharani", "Gayathri", 
                    "Hema", "Jeevitha", "Jeyanandhini", "Kaleeswari", "Karthika", "Mathavi", 
                    "Nithiya", "Pandiselvi", "Reshma", "Sujitha", "Swathi", "Varshini", 
                    "Lakshmipriya", "Priyadharshini.K"
                };

                string password = "vendhan@123";
                string passwordHash = HashPassword(password);

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    Console.WriteLine("Connected to database.");

                        // --- Ensure Schema is up to date ---
                        try {
                            using (var cmd = new NpgsqlCommand("ALTER TABLE Projects ADD COLUMN IF NOT EXISTS MasterFilePassword TEXT", connection))
                                cmd.ExecuteNonQuery();
                            using (var cmd = new NpgsqlCommand("ALTER TABLE Projects ADD COLUMN IF NOT EXISTS BillingFormula TEXT", connection))
                                cmd.ExecuteNonQuery();
                            using (var cmd = new NpgsqlCommand("ALTER TABLE Projects ADD COLUMN IF NOT EXISTS MasterFilePath TEXT", connection))
                                cmd.ExecuteNonQuery();
                            using (var cmd = new NpgsqlCommand("ALTER TABLE Projects ADD COLUMN IF NOT EXISTS QualityBasePath TEXT", connection))
                                cmd.ExecuteNonQuery();
                        } catch { }

                        foreach (var name in names)
                    {
                        string username = name.Replace(".", "").Replace(" ", "").ToLower();
                        string email = $"{username}@company.com";

                        // Check if user exists
                        using (var checkCmd = new NpgsqlCommand("SELECT COUNT(1) FROM Users WHERE Username = @u", connection))
                        {
                            checkCmd.Parameters.AddWithValue("u", username);
                            long count = (long)checkCmd.ExecuteScalar();
                            if (count > 0)
                            {
                                Console.WriteLine($"User {username} already exists, skipping.");
                                continue;
                            }
                        }

                        string sql = @"
                            INSERT INTO Users (Username, PasswordHash, Role, FullName, Email, IsActive, CreatedAt)
                            VALUES (@u, @p, 'User', @f, @e, 1, CURRENT_TIMESTAMP)";
                        
                        using (var command = new NpgsqlCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("u", username);
                            command.Parameters.AddWithValue("p", passwordHash);
                            command.Parameters.AddWithValue("f", name);
                            command.Parameters.AddWithValue("e", email);
                            command.ExecuteNonQuery();
                            Console.WriteLine($"Inserted user: {name} ({username})");
                        }
                    }
                    // --- Project Updates ---
                    var projects = new[]
                    {
                        new { 
                            Name = "Gamma142", 
                            Master = @"\\vendhansvr1\e$\Gamma142.xlsx", 
                            Pass = "142", 
                            Quality = @"\\vendhansvr1\e$\Gamma142\Quality_Report",
                            Formula = "(charactercount / 1000) * 4.95",
                            Fields = new[] { ("object_Id", "Text", 1, 0), ("charactercount", "Number", 1, 1) }
                        },
                        new { 
                            Name = "Gamma143", 
                            Master = @"\\vendhansvr1\e$\Gamma143.xlsx", 
                            Pass = "143", 
                            Quality = @"\\vendhansvr1\e$\Gamma143\Quality_Report",
                            Formula = "((pages * 0.05) + ((charactercount / 1000) * 4.95))",
                            Fields = new[] { ("object_Id", "Text", 1, 0), ("pages", "Number", 1, 1), ("charactercount", "Number", 1, 1) }
                        }
                    };

                    foreach (var p in projects)
                    {
                        Console.WriteLine($"\nSyncing Project: {p.Name}...");
                        long projectId = 0;
                        
                        using (var cmd = new NpgsqlCommand("SELECT Id FROM Projects WHERE Name = @n", connection))
                        {
                            cmd.Parameters.AddWithValue("n", p.Name);
                            var result = cmd.ExecuteScalar();
                            if (result != null) projectId = (int)result;
                        }

                        if (projectId == 0)
                        {
                            string insertSql = @"INSERT INTO Projects (Name, Description, IsActive, MasterFilePath, MasterFilePassword, QualityBasePath, BillingFormula)
                                                VALUES (@n, @d, 1, @m, @p, @q, @f) RETURNING Id";
                            using (var cmd = new NpgsqlCommand(insertSql, connection))
                            {
                                cmd.Parameters.AddWithValue("n", p.Name);
                                cmd.Parameters.AddWithValue("d", $"Project {p.Name} live data");
                                cmd.Parameters.AddWithValue("m", p.Master);
                                cmd.Parameters.AddWithValue("p", p.Pass);
                                cmd.Parameters.AddWithValue("q", p.Quality);
                                cmd.Parameters.AddWithValue("f", p.Formula);
                                projectId = (int)cmd.ExecuteScalar();
                                Console.WriteLine($"Created project: {p.Name} (ID: {projectId})");
                            }
                        }
                        else
                        {
                            string updateSql = @"UPDATE Projects SET MasterFilePath=@m, MasterFilePassword=@p, QualityBasePath=@q, BillingFormula=@f WHERE Id=@id";
                            using (var cmd = new NpgsqlCommand(updateSql, connection))
                            {
                                cmd.Parameters.AddWithValue("m", p.Master);
                                cmd.Parameters.AddWithValue("p", p.Pass);
                                cmd.Parameters.AddWithValue("q", p.Quality);
                                cmd.Parameters.AddWithValue("f", p.Formula);
                                cmd.Parameters.AddWithValue("id", projectId);
                                cmd.ExecuteNonQuery();
                                Console.WriteLine($"Updated project: {p.Name}");
                            }
                        }

                        // Sync Fields
                        using (var cmd = new NpgsqlCommand("DELETE FROM ProjectFields WHERE ProjectId = @id", connection))
                        {
                            cmd.Parameters.AddWithValue("id", projectId);
                            cmd.ExecuteNonQuery();
                        }

                        int sortOrder = 1;
                        foreach (var field in p.Fields)
                        {
                            string fieldSql = @"INSERT INTO ProjectFields (ProjectId, FieldLabel, FieldType, IsRequired, IncludeInBilling, SortOrder)
                                              VALUES (@id, @l, @t, @req, @bill, @s)";
                            using (var cmd = new NpgsqlCommand(fieldSql, connection))
                            {
                                cmd.Parameters.AddWithValue("id", projectId);
                                cmd.Parameters.AddWithValue("l", field.Item1);
                                cmd.Parameters.AddWithValue("t", field.Item2);
                                cmd.Parameters.AddWithValue("req", field.Item3);
                                cmd.Parameters.AddWithValue("bill", field.Item4);
                                cmd.Parameters.AddWithValue("s", sortOrder++);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        Console.WriteLine($"Updated fields for {p.Name}");
                    }

                    // --- Master Data Sync ---
                    Console.WriteLine("\n--- Triggering Master Data Sync ---");
                    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                    foreach (var p in projects)
                    {
                        if (string.IsNullOrEmpty(p.Master)) continue;
                        
                        Console.WriteLine($"Syncing Master File for {p.Name}: {p.Master}");
                        try 
                        {
                            if (!File.Exists(p.Master))
                            {
                                Console.WriteLine($"  Warning: File not found or inaccessible: {p.Master}");
                                continue;
                            }

                            using (var stream = File.Open(p.Master, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                var config = new ExcelReaderConfiguration();
                                if (!string.IsNullOrEmpty(p.Pass)) config.Password = p.Pass;

                                using (var reader = ExcelReaderFactory.CreateReader(stream, config))
                                {
                                    var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                                    {
                                        ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                                    });

                                    if (ds.Tables.Count > 0)
                                    {
                                        var dt = ds.Tables[0];
                                        int imported = 0;
                                        
                                        // Get ProjectId again to be sure
                                        long projectId = 0;
                                        using (var cmd = new NpgsqlCommand("SELECT Id FROM Projects WHERE Name = @n", connection))
                                        {
                                            cmd.Parameters.AddWithValue("n", p.Name);
                                            var res = cmd.ExecuteScalar();
                                            if (res != null) projectId = (int)res;
                                        }

                                        foreach (DataRow row in dt.Rows)
                                        {
                                            string objectId = GetColumnValue(row, "Object_ID", "ObjectId", "Object ID");
                                            if (string.IsNullOrWhiteSpace(objectId)) continue;

                                            string sql = @"INSERT INTO MasterData (ProjectId, ManifestId, ObjectId, ObjectName, Pages, CharacterCount, Status, ImportDate)
                                                           VALUES (@pid, @mid, @oid, @name, @p, @c, 'Unassigned', CURRENT_TIMESTAMP)
                                                           ON CONFLICT (ProjectId, ObjectId) DO UPDATE SET 
                                                           ManifestId = EXCLUDED.ManifestId,
                                                           ObjectName = EXCLUDED.ObjectName,
                                                           Pages = EXCLUDED.Pages,
                                                           CharacterCount = EXCLUDED.CharacterCount";

                                            using (var cmd = new NpgsqlCommand(sql, connection))
                                            {
                                                cmd.Parameters.AddWithValue("pid", projectId);
                                                cmd.Parameters.AddWithValue("mid", GetColumnValue(row, "Manifest_ID", "ManifestId", "Manifest ID"));
                                                cmd.Parameters.AddWithValue("oid", objectId);
                                                cmd.Parameters.AddWithValue("name", GetColumnValue(row, "Name", "ObjectName", "Object Name"));
                                                cmd.Parameters.AddWithValue("p", GetIntValue(row, "Pages", "No_of_Pages"));
                                                cmd.Parameters.AddWithValue("c", GetIntValue(row, "Charactercount", "Character_Count", "Characters"));
                                                cmd.ExecuteNonQuery();
                                            }
                                            imported++;
                                        }
                                        Console.WriteLine($"  Successfully synced {imported} records for {p.Name}.");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Error syncing {p.Name}: {ex.Message}");
                        }
                    }
                }
                Console.WriteLine("\nOperation completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private static string GetColumnValue(DataRow row, params string[] names)
        {
            foreach (var name in names)
            {
                if (row.Table.Columns.Contains(name)) return row[name]?.ToString() ?? "";
                // Try case-insensitive
                foreach (DataColumn col in row.Table.Columns)
                {
                    if (col.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase)) return row[col]?.ToString() ?? "";
                }
            }
            return "";
        }

        private static int GetIntValue(DataRow row, params string[] names)
        {
            var val = GetColumnValue(row, names);
            if (int.TryParse(val, out int result)) return result;
            return 0;
        }

        private static string HashPassword(string password)
        {
            int iterations = 100000;
            int saltSize = 32;
            int hashSize = 32;

            byte[] salt = new byte[saltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(hashSize);
                string saltBase64 = Convert.ToBase64String(salt);
                string hashBase64 = Convert.ToBase64String(hash);
                return $"{iterations}.{saltBase64}.{hashBase64}";
            }
        }
    }
}
