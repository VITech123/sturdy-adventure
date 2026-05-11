using System;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using EnterpriseWorkReport.Models;

namespace EnterpriseWorkReport.Services
{
    public class AuthService
    {
        private const int SaltSize = 32; // 256 bits
        private const int HashSize = 32; // 256 bits
        private const int Iterations = 100000; // PBKDF2 iterations
        
        /// <summary>
        /// Hash a password using PBKDF2 with random salt (much more secure than SHA256)
        /// Format: iterations.salt.hash (base64 encoded)
        /// </summary>
        public static string HashPassword(string password)
        {
            // Generate random salt
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            
            // Generate hash using PBKDF2
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(HashSize);
                
                // Combine: iterations.salt.hash
                string saltBase64 = Convert.ToBase64String(salt);
                string hashBase64 = Convert.ToBase64String(hash);
                
                return $"{Iterations}.{saltBase64}.{hashBase64}";
            }
        }

        /// <summary>
        /// Verify a password against a hash
        /// Supports both new PBKDF2 format and legacy SHA256 format
        /// </summary>
        public static bool VerifyPassword(string password, string hash)
        {
            // Check if it's the new PBKDF2 format
            if (hash.Contains("."))
            {
                return VerifyPBKDF2Password(password, hash);
            }
            
            // Legacy SHA256 verification (for backwards compatibility)
            return VerifyLegacyPassword(password, hash);
        }
        
        private static bool VerifyPBKDF2Password(string password, string hash)
        {
            try
            {
                string[] parts = hash.Split('.');
                if (parts.Length != 3) return false;
                
                int iterations = int.Parse(parts[0]);
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] hashBytes = Convert.FromBase64String(parts[2]);
                
                // Use SHA256 to match HashPassword method
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                {
                    byte[] testHash = pbkdf2.GetBytes(hashBytes.Length);
                    
                    // Constant-time comparison to prevent timing attacks
                    return ConstantTimeEquals(hashBytes, testHash);
                }
            }
            catch
            {
                return false;
            }
        }
        
        private static bool VerifyLegacyPassword(string password, string hash)
        {
            using (var sha256 = SHA256.Create())
            {
                string saltedPassword = $"EWR_{password}_2024";
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
                StringBuilder result = new StringBuilder();
                foreach (byte b in bytes)
                    result.Append(b.ToString("x2"));
                return result.ToString() == hash;
            }
        }
        
        /// <summary>
        /// Constant-time comparison to prevent timing attacks
        /// </summary>
        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            
            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }

        public User Login(string username, string password)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                try
                {
                    var user = conn.QueryFirstOrDefault<User>(
                        "SELECT * FROM Users WHERE Username = @Username AND IsActive = 1",
                        new { Username = username });

                    if (user == null)
                        return null;

                    // Verify password using SHA256 hash
                    if (!VerifyPassword(password, user.PasswordHash))
                        return null;

                    // Update last login time
                    conn.Execute(
                        "UPDATE Users SET LastLoginAt = @LastLogin WHERE Id = @Id",
                        new { LastLogin = DateTime.Now, Id = user.Id });

                    return user;
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data", "error.log"),
                        $"\n[Login Error] {DateTime.Now}: {ex.Message}\n{ex.StackTrace}\n");
                    throw;
                }
            }
        }

        public bool ChangePassword(int userId, string currentPassword, string newPassword)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var user = conn.QueryFirstOrDefault<User>(
                    "SELECT * FROM Users WHERE Id = @Id",
                    new { Id = userId });

                if (user == null)
                    return false;

                // Verify current password
                if (!VerifyPassword(currentPassword, user.PasswordHash))
                    return false;

                // Hash and update new password
                string newHash = HashPassword(newPassword);
                int rows = conn.Execute(
                    "UPDATE Users SET PasswordHash = @Hash WHERE Id = @Id",
                    new { Hash = newHash, Id = userId });

                if (rows > 0)
                {
                    AuditService.Log("Password Changed", $"User ID {userId} changed their password");
                }
                
                return rows > 0;
            }
        }

        /// <summary>
        /// Admin reset password - sets a new password without verifying current
        /// </summary>
        public bool AdminResetPassword(int userId, string newPassword)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                string newHash = HashPassword(newPassword);
                int rows = conn.Execute(
                    "UPDATE Users SET PasswordHash = @Hash WHERE Id = @Id",
                    new { Hash = newHash, Id = userId });

                if (rows > 0)
                {
                    AuditService.Log("Admin Password Reset", $"Admin reset password for User ID {userId}");
                }
                
                return rows > 0;
            }
        }

        /// <summary>
        /// Update user profile
        /// </summary>
        public bool UpdateProfile(int userId, string fullName, string email, string phone, string place, string education, string emergency, string department, string designation, string profilePicture = null, string wallpaperPath = null, string selectedTheme = null)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                int rows = conn.Execute(
                    @"UPDATE Users SET FullName = @FullName, Email = @Email, Phone = @Phone, 
                      Place = @Place, Education = @Education, EmergencyNumber = @Emergency,
                      Department = @Department, Designation = @Designation, ProfilePicture = @ProfilePicture,
                      WallpaperPath = @WallpaperPath, SelectedTheme = @SelectedTheme WHERE Id = @Id",
                    new { FullName = fullName, Email = email, Phone = phone, 
                          Place = place, Education = education, Emergency = emergency,
                          Department = department, Designation = designation, ProfilePicture = profilePicture,
                          WallpaperPath = wallpaperPath, SelectedTheme = selectedTheme, Id = userId });

                if (rows > 0)
                {
                    AuditService.Log("Profile Updated", $"User ID {userId} updated their profile");
                }
                
                return rows > 0;
            }
        }
    }
}
