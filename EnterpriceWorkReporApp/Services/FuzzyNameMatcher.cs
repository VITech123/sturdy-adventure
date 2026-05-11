using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;

namespace EnterpriseWorkReport.Services
{
    /// <summary>
    /// Fuzzy name matching service that resolves employee names from quality reports
    /// and master files against a canonical MasterNames list.
    /// 
    /// Rules:
    /// - Suffix-aware: "Deepa.T" and "Deepa.M" are DIFFERENT people
    /// - Case-insensitive: "angeeswari" = "Angeeswari"
    /// - Space/format tolerant: "Priya Dharshini" = "priyadharshini" = "Priydhrshini"
    /// - Typo tolerant via Levenshtein distance (threshold >= 0.75)
    /// </summary>
    public class FuzzyNameMatcher
    {
        private readonly List<string> _masterNames = new List<string>();
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _isLoaded;

        /// <summary>
        /// Load canonical names from MasterNames.xlsx
        /// </summary>
        public void LoadMasterNames(string masterNamesPath)
        {
            _masterNames.Clear();
            _cache.Clear();
            _isLoaded = false;

            if (string.IsNullOrEmpty(masterNamesPath) || !File.Exists(masterNamesPath))
                return;

            try
            {
                using (var stream = File.Open(masterNamesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                    });

                    if (ds.Tables.Count > 0)
                    {
                        var table = ds.Tables[0];
                        foreach (DataRow row in table.Rows)
                        {
                            string name = row[0]?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                // Avoid duplicates
                                if (!_masterNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
                                    _masterNames.Add(name);
                            }
                        }
                    }
                }
                _isLoaded = _masterNames.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FuzzyNameMatcher: Failed to load master names: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns true if master names have been loaded successfully.
        /// </summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>
        /// Number of canonical names loaded.
        /// </summary>
        public int MasterNameCount => _masterNames.Count;

        /// <summary>
        /// Resolve an input name to the best-matching canonical name from MasterNames.
        /// Returns the canonical name if found, or the original input if no match.
        /// </summary>
        public string Resolve(string inputName)
        {
            if (string.IsNullOrWhiteSpace(inputName))
                return inputName;

            string trimmed = inputName.Trim();

            // Check cache first
            if (_cache.TryGetValue(trimmed, out var cached))
                return cached;

            // If no master names loaded, return as-is with title case
            if (!_isLoaded)
            {
                string titleCased = ToTitleCase(trimmed);
                _cache[trimmed] = titleCased;
                return titleCased;
            }

            string result = FindBestMatch(trimmed);
            _cache[trimmed] = result;
            return result;
        }

        /// <summary>
        /// Core matching logic with suffix-awareness.
        /// </summary>
        private string FindBestMatch(string input)
        {
            // Parse input into base name and suffix
            ParseName(input, out string inputBase, out string inputSuffix);

            // Phase 1: Exact match (case-insensitive)
            foreach (var master in _masterNames)
            {
                if (master.Equals(input, StringComparison.OrdinalIgnoreCase))
                    return master;
            }

            // Phase 2: Normalized exact match (remove spaces, compare base)
            string inputNorm = NormalizeName(inputBase);
            
            string bestMatch = null;
            double bestScore = 0;

            foreach (var master in _masterNames)
            {
                ParseName(master, out string masterBase, out string masterSuffix);

                // Suffix constraint: if both have suffixes, they must match
                if (!string.IsNullOrEmpty(inputSuffix) && !string.IsNullOrEmpty(masterSuffix))
                {
                    if (!inputSuffix.Equals(masterSuffix, StringComparison.OrdinalIgnoreCase))
                        continue; // Hard reject: different suffixes
                }
                // If input has a suffix but master doesn't, skip (input is more specific)
                else if (!string.IsNullOrEmpty(inputSuffix) && string.IsNullOrEmpty(masterSuffix))
                {
                    // Allow matching only if there's no other master with a matching suffix
                    // We'll handle this by scoring lower
                }

                string masterNorm = NormalizeName(masterBase);

                // Exact normalized match
                if (inputNorm.Equals(masterNorm, StringComparison.OrdinalIgnoreCase))
                {
                    // Perfect base match — check suffix compatibility
                    if (!string.IsNullOrEmpty(inputSuffix) && !string.IsNullOrEmpty(masterSuffix)
                        && inputSuffix.Equals(masterSuffix, StringComparison.OrdinalIgnoreCase))
                        return master; // Perfect match including suffix

                    if (string.IsNullOrEmpty(inputSuffix) && string.IsNullOrEmpty(masterSuffix))
                        return master; // Both have no suffix

                    // One has suffix, other doesn't — score 0.95
                    double score = 0.95;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMatch = master;
                    }
                    continue;
                }

                // Phase 3: Levenshtein similarity
                double similarity = ComputeSimilarity(inputNorm, masterNorm);
                if (similarity >= 0.75 && similarity > bestScore)
                {
                    bestScore = similarity;
                    bestMatch = master;
                }
            }

            if (bestMatch != null && bestScore >= 0.75)
                return bestMatch;

            // No match found — return title-cased input
            return ToTitleCase(input);
        }

        /// <summary>
        /// Parse a name into base and suffix. 
        /// E.g., "Deepa.T" -> base="Deepa", suffix="T"
        /// "Priyadharshini.K" -> base="Priyadharshini", suffix="K"
        /// "Angeeswari" -> base="Angeeswari", suffix=""
        /// </summary>
        private static void ParseName(string name, out string baseName, out string suffix)
        {
            if (string.IsNullOrEmpty(name))
            {
                baseName = "";
                suffix = "";
                return;
            }

            // Look for suffix pattern: single char after last dot (e.g., ".T", ".K", ".M")
            int lastDot = name.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < name.Length - 1)
            {
                string afterDot = name.Substring(lastDot + 1).Trim();
                // Suffix is only valid if it's 1-2 characters (initial)
                if (afterDot.Length <= 2)
                {
                    baseName = name.Substring(0, lastDot).Trim();
                    suffix = afterDot;
                    return;
                }
            }

            baseName = name;
            suffix = "";
        }

        /// <summary>
        /// Normalize a name: lowercase, remove spaces, remove dots, remove underscores.
        /// </summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder(name.Length);
            foreach (char c in name.ToLowerInvariant())
            {
                if (char.IsLetter(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Compute similarity ratio between two strings using Levenshtein distance.
        /// Returns a value between 0.0 (completely different) and 1.0 (identical).
        /// </summary>
        public static double ComputeSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

            int distance = LevenshteinDistance(s1.ToLowerInvariant(), s2.ToLowerInvariant());
            int maxLen = Math.Max(s1.Length, s2.Length);
            return 1.0 - (double)distance / maxLen;
        }

        /// <summary>
        /// Compute the Levenshtein edit distance between two strings.
        /// </summary>
        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        /// <summary>
        /// Convert to title case (first letter uppercase, rest lowercase).
        /// </summary>
        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            
            // Handle suffix names like "deepa.t" → "Deepa.T"
            ParseName(input, out string basePart, out string suffixPart);
            
            string titleBase = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(basePart.ToLower());
            
            if (!string.IsNullOrEmpty(suffixPart))
                return titleBase + "." + suffixPart.ToUpper();
            
            return titleBase;
        }

        /// <summary>
        /// Get all loaded master names for display/debugging.
        /// </summary>
        public IReadOnlyList<string> GetMasterNames() => _masterNames.AsReadOnly();
    }
}
