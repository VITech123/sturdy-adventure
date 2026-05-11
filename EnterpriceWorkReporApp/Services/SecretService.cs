using System;
using System.Security.Cryptography;
using System.Text;

namespace EnterpriseWorkReport.Services
{
    public static class SecretService
    {
        private const string Prefix = "DPAPI:";

        public static string EncryptSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            try
            {
                var plaintext = Encoding.UTF8.GetBytes(value);
                var cipher = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
                return Prefix + Convert.ToBase64String(cipher);
            }
            catch
            {
                return value;
            }
        }

        public static string DecryptSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            if (!value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return value;

            try
            {
                var cipher = Convert.FromBase64String(value.Substring(Prefix.Length));
                var plaintext = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plaintext);
            }
            catch
            {
                return value;
            }
        }

        public static string EnsureEncrypted(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return value;

            return EncryptSecret(value);
        }
    }
}
