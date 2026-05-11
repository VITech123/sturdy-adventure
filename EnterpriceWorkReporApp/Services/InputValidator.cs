using System;
using System.Text.RegularExpressions;

namespace EnterpriseWorkReport.Services
{
    /// <summary>
    /// Provides input validation and sanitization to prevent security vulnerabilities
    /// </summary>
    public static class InputValidator
    {
        // Patterns for common attacks - simplified patterns to avoid escaping issues
        private static readonly Regex SqlInjectionPattern = new Regex(
            @"(--|;|union|select|insert|update|delete|drop|create|alter|exec|execute)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        private static readonly Regex XssPattern = new Regex(
            @"(<script|javascript:|on\w+\s*=|<iframe|<object|<embed|alert\s*\()",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        private static readonly Regex PathTraversalPattern = new Regex(
            @"(\.\./|\.\.\\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        /// <summary>
        /// Validates username format (alphanumeric, underscore, hyphen, 3-50 chars)
        /// </summary>
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;
            
            // Username: 3-50 chars, alphanumeric + underscore + hyphen
            return Regex.IsMatch(username, @"^[a-zA-Z0-9_-]{3,50}$");
        }
        
        /// <summary>
        /// Validates password strength
        /// </summary>
        public static (bool IsValid, string Message) ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
                return (false, "Password is required.");
            
            if (password.Length < 8)
                return (false, "Password must be at least 8 characters long.");
            
            if (!Regex.IsMatch(password, @"[A-Z]"))
                return (false, "Password must contain at least one uppercase letter.");
            
            if (!Regex.IsMatch(password, @"[a-z]"))
                return (false, "Password must contain at least one lowercase letter.");
            
            if (!Regex.IsMatch(password, @"[0-9]"))
                return (false, "Password must contain at least one digit.");
            
            if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
                return (false, "Password must contain at least one special character.");
            
            return (true, "Password is strong.");
        }
        
        /// <summary>
        /// Validates email format
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;
            
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Validates phone number (basic international format)
        /// </summary>
        public static bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return true; // Phone is optional
            
            // Allow +, numbers, spaces, hyphens, parentheses
            return Regex.IsMatch(phone, @"^[\+\d\s\-\(\)]{7,20}$");
        }
        
        /// <summary>
        /// Checks for potential SQL injection attempts
        /// </summary>
        public static bool ContainsSqlInjection(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
            
            return SqlInjectionPattern.IsMatch(input);
        }
        
        /// <summary>
        /// Checks for potential XSS attempts
        /// </summary>
        public static bool ContainsXss(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
            
            return XssPattern.IsMatch(input);
        }
        
        /// <summary>
        /// Checks for path traversal attempts
        /// </summary>
        public static bool ContainsPathTraversal(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
            
            return PathTraversalPattern.IsMatch(input);
        }
        
        /// <summary>
        /// Validates input for common security threats
        /// </summary>
        public static (bool IsSafe, string ThreatType) ValidateSecurity(string input)
        {
            if (string.IsNullOrEmpty(input))
                return (true, null);
            
            if (ContainsSqlInjection(input))
                return (false, "SQL Injection");
            
            if (ContainsXss(input))
                return (false, "Cross-Site Scripting (XSS)");
            
            if (ContainsPathTraversal(input))
                return (false, "Path Traversal");
            
            return (true, null);
        }
        
        /// <summary>
        /// Validates file name to prevent path traversal
        /// </summary>
        public static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;
            
            // Check for invalid characters
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            if (fileName.IndexOfAny(invalidChars) >= 0)
                return false;
            
            // Check for path traversal
            if (ContainsPathTraversal(fileName))
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Validates a date range
        /// </summary>
        public static bool IsValidDateRange(DateTime startDate, DateTime endDate)
        {
            return startDate <= endDate && endDate <= DateTime.Now.AddYears(1);
        }
        
        /// <summary>
        /// Truncates string to maximum length
        /// </summary>
        public static string Truncate(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input;
            
            return input.Substring(0, maxLength);
        }
    }
}
