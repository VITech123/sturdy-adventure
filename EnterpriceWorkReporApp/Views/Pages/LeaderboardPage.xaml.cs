using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class LeaderboardPage : Page
    {
        private bool _isInitialized = false;
        private string _activeTab = "Billing";
        
        public LeaderboardPage()
        {
            InitializeComponent();
            // Set default selection after InitializeComponent to avoid XAML initialization events
            if (PeriodSelector.Items.Count > 0)
                PeriodSelector.SelectedIndex = 0;
            _isInitialized = true;
            LoadLeaderboard();
        }

        private void Period_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized)
                LoadLeaderboard();
        }

        private void TabBilling_Click(object sender, RoutedEventArgs e)
        {
            _activeTab = "Billing";
            TabBilling.Style = (Style)FindResource("PrimaryButton");
            TabQuality.Style = (Style)FindResource("OutlineButton");
            TabWorkCount.Style = (Style)FindResource("OutlineButton");
            ColBilling.Visibility = Visibility.Visible;
            ColQuality.Visibility = Visibility.Collapsed;
            LoadLeaderboard();
        }

        private void TabQuality_Click(object sender, RoutedEventArgs e)
        {
            _activeTab = "Quality";
            TabBilling.Style = (Style)FindResource("OutlineButton");
            TabQuality.Style = (Style)FindResource("PrimaryButton");
            TabWorkCount.Style = (Style)FindResource("OutlineButton");
            ColBilling.Visibility = Visibility.Collapsed;
            ColQuality.Visibility = Visibility.Visible;
            LoadLeaderboard();
        }

        private void TabWorkCount_Click(object sender, RoutedEventArgs e)
        {
            _activeTab = "WorkCount";
            TabBilling.Style = (Style)FindResource("OutlineButton");
            TabQuality.Style = (Style)FindResource("OutlineButton");
            TabWorkCount.Style = (Style)FindResource("PrimaryButton");
            ColBilling.Visibility = Visibility.Collapsed;
            ColQuality.Visibility = Visibility.Collapsed;
            LoadLeaderboard();
        }

        private void LoadLeaderboard()
        {
            if (LeaderboardGrid == null || !_isInitialized) return;

            try
            {
                string wrDateFilter = "";
                string qrDateFilter = "";
                var period = (PeriodSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Today";

                switch (period)
                {
                    case "Today":
                    case "Daily":
                        wrDateFilter = "AND DATE(wr.SubmissionDate) = CURRENT_DATE";
                        qrDateFilter = "AND qr.ReportDate = CURRENT_DATE";
                        break;
                    case "This Week":
                    case "Weekly":
                        wrDateFilter = "AND wr.SubmissionDate >= CURRENT_DATE - INTERVAL '7 days'";
                        qrDateFilter = "AND qr.ReportDate >= CURRENT_DATE - INTERVAL '7 days'";
                        break;
                    case "This Month":
                    case "Monthly":
                        wrDateFilter = "AND to_char(wr.SubmissionDate, 'YYYY-MM') = to_char(CURRENT_DATE, 'YYYY-MM')";
                        qrDateFilter = "AND to_char(qr.ReportDate, 'YYYY-MM') = to_char(CURRENT_DATE, 'YYYY-MM')";
                        break;
                    default:
                        wrDateFilter = "";
                        qrDateFilter = "";
                        break;
                }

                string sql;
                if (_activeTab == "Quality")
                {
                    sql = $@"
                        SELECT u.FullName AS ""FullName"",
                               COALESCE(AVG(qr.QualityScore), 0) AS ""QualityScore"",
                               COUNT(qr.Id) AS ""ReportCount"",
                               0.0 AS ""BillingTotal""
                        FROM Users u
                        LEFT JOIN QualityReports qr ON qr.UserId = u.Id {qrDateFilter}
                        WHERE u.Role != 'Administrator' AND u.IsActive = 1
                        GROUP BY u.Id, u.FullName
                        HAVING COUNT(qr.Id) > 0
                        ORDER BY ""QualityScore"" DESC";
                }
                else if (_activeTab == "WorkCount")
                {
                    sql = $@"
                        SELECT u.FullName AS ""FullName"",
                               COUNT(wr.Id) AS ""ReportCount"",
                               COALESCE(SUM(wr.BillingAmount), 0) AS ""BillingTotal"",
                               0.0 AS ""QualityScore""
                        FROM Users u
                        LEFT JOIN WorkReports wr ON wr.UserId = u.Id {wrDateFilter}
                        WHERE u.Role != 'Administrator' AND u.IsActive = 1
                        GROUP BY u.Id, u.FullName
                        HAVING COUNT(wr.Id) > 0
                        ORDER BY ""ReportCount"" DESC";
                }
                else // Billing
                {
                    sql = $@"
                        SELECT u.FullName AS ""FullName"",
                               COALESCE(SUM(wr.BillingAmount), 0) AS ""BillingTotal"",
                               COUNT(wr.Id) AS ""ReportCount"",
                               0.0 AS ""QualityScore""
                        FROM Users u
                        LEFT JOIN WorkReports wr ON wr.UserId = u.Id {wrDateFilter}
                        WHERE u.Role != 'Administrator' AND u.IsActive = 1
                        GROUP BY u.Id, u.FullName
                        HAVING COUNT(wr.Id) > 0
                        ORDER BY ""BillingTotal"" DESC";
                }

                using (var conn = DatabaseService.GetConnection())
                {
                    var rawResults = conn.Query(sql).AsList();
                    string[] medals = { "🥇", "🥈", "🥉" };

                    var results = rawResults.Select((row, idx) =>
                    {
                        var dict = (IDictionary<string, object>)row;
                        int rank = idx + 1;
                        string medal = rank <= 3 ? medals[rank - 1] : rank.ToString();
                        return new
                        {
                            Rank        = rank,
                            RankDisplay = medal,
                            FullName    = dict.ContainsKey("FullName") ? dict["FullName"]?.ToString() ?? "Unknown" : "Unknown",
                            BillingTotal = dict.ContainsKey("BillingTotal") && dict["BillingTotal"] != null ? Convert.ToDouble(dict["BillingTotal"]) : 0.0,
                            ReportCount  = dict.ContainsKey("ReportCount")  && dict["ReportCount"]  != null ? Convert.ToInt32(dict["ReportCount"])  : 0,
                            QualityScore = dict.ContainsKey("QualityScore") && dict["QualityScore"] != null ? Convert.ToDouble(dict["QualityScore"]) : 0.0
                        };
                    }).ToList();

                    LeaderboardGrid.ItemsSource = results;

                    // Update podium (top 3 by billing)
                    if (Rank1Name != null) UpdatePodium(results.Count > 0 ? (object)results[0] : null, Rank1Name, Rank1Billing, Rank1Initial);
                    if (Rank2Name != null) UpdatePodium(results.Count > 1 ? (object)results[1] : null, Rank2Name, Rank2Billing, Rank2Initial);
                    if (Rank3Name != null) UpdatePodium(results.Count > 2 ? (object)results[2] : null, Rank3Name, Rank3Billing, Rank3Initial);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading leaderboard:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePodium(object entry, TextBlock nameBlock, TextBlock billingBlock, TextBlock initialBlock)
        {
            if (entry == null)
            {
                nameBlock.Text = "—";
                billingBlock.Text = "₹0";
                initialBlock.Text = "?";
                return;
            }

            // Works for both dynamic Dapper rows and anonymous types
            string fullName = "";
            double billingTotal = 0;
            if (entry is IDictionary<string, object> dict)
            {
                fullName = dict.ContainsKey("FullName") ? dict["FullName"]?.ToString() ?? "" : "";
                billingTotal = dict.ContainsKey("BillingTotal") ? Convert.ToDouble(dict["BillingTotal"]) : 0;
            }
            else
            {
                // Anonymous type via reflection
                var t = entry.GetType();
                fullName = t.GetProperty("FullName")?.GetValue(entry)?.ToString() ?? "";
                var bt = t.GetProperty("BillingTotal")?.GetValue(entry);
                billingTotal = bt != null ? Convert.ToDouble(bt) : 0;
            }

            nameBlock.Text = string.IsNullOrEmpty(fullName) ? "—" : fullName;
            billingBlock.Text = $"₹{billingTotal:F2}";
            initialBlock.Text = fullName.Length > 0 ? fullName[0].ToString().ToUpper() : "?";
        }
    }
}
