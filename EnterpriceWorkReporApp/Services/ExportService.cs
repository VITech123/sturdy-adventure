using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using EnterpriseWorkReport.Models;

namespace EnterpriseWorkReport.Services
{
    public static class ExportService
    {
        private static string GetLogoBase64()
        {
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo", "LOGO.png");
                if (File.Exists(logoPath))
                {
                    byte[] bytes = File.ReadAllBytes(logoPath);
                    return "data:image/png;base64," + Convert.ToBase64String(bytes);
                }
            }
            catch { }
            return "";
        }

        private static string GetHtmlHeader(string title)
        {
            string logo = GetLogoBase64();
            string companyName = "Enterprise Work Report System";
            
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'>");
            sb.AppendLine($"<title>{title}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
                body { font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; margin: 0; padding: 40px; color: #333; line-height: 1.6; }
                .header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 3px solid #1a73e8; padding-bottom: 20px; margin-bottom: 30px; }
                .logo-container { max-width: 250px; }
                .logo-container img { max-width: 100%; height: auto; }
                .company-info { text-align: right; }
                .company-info h1 { margin: 0; color: #1a73e8; font-size: 24px; }
                .company-info p { margin: 5px 0 0 0; color: #666; font-size: 14px; }
                .report-info { margin-bottom: 30px; }
                .report-info h2 { margin: 0; color: #444; text-transform: uppercase; letter-spacing: 1px; }
                .report-info p { margin: 5px 0 0 0; color: #888; font-size: 13px; }
                table { width: 100%; border-collapse: collapse; margin-top: 10px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
                th, td { padding: 12px 15px; text-align: left; border-bottom: 1px solid #eee; }
                th { background-color: #f8f9fa; color: #555; font-weight: 600; text-transform: uppercase; font-size: 12px; letter-spacing: 0.5px; }
                tr:hover { background-color: #fcfcfc; }
                .total-section { margin-top: 30px; text-align: right; border-top: 2px solid #eee; padding-top: 15px; }
                .total-label { font-size: 16px; color: #666; }
                .total-amount { font-size: 24px; color: #1a73e8; font-weight: bold; margin-left: 15px; }
                .signature-section { margin-top: 80px; display: flex; justify-content: space-between; }
                .signature-box { width: 250px; border-top: 1px solid #999; text-align: center; padding-top: 10px; color: #777; font-size: 13px; }
                .footer { margin-top: 50px; text-align: center; font-size: 11px; color: #aaa; border-top: 1px solid #eee; padding-top: 20px; }
                @media print {
                    body { padding: 0; }
                    .header { border-bottom-color: #1a73e8 !important; -webkit-print-color-adjust: exact; }
                }
            ");
            sb.AppendLine("</style></head><body>");
            
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<div class='logo-container'>");
            if (!string.IsNullOrEmpty(logo))
                sb.AppendLine($"<img src='{logo}' alt='Company Logo'>");
            else
                sb.AppendLine("<h1 style='color:#1a73e8;margin:0;'>ENTERPRISE</h1>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class='company-info'>");
            sb.AppendLine($"<h1>{companyName}</h1>");
            sb.AppendLine("<p>Automated Digital Data Services</p>");
            sb.AppendLine("<p>Secure. Accurate. Efficient.</p>");
            sb.AppendLine("</div></div>");
            
            sb.AppendLine("<div class='report-info'>");
            sb.AppendLine($"<h2>{title}</h2>");
            sb.AppendLine($"<p>Generated on {DateTime.Now:MMMM dd, yyyy HH:mm}</p>");
            sb.AppendLine("</div>");

            return sb.ToString();
        }

        public static void ExportBillingToHtml(IEnumerable<WorkReport> records, string filePath)
        {
            var sb = new StringBuilder();
            sb.Append(GetHtmlHeader("Billing Statement"));
            
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>Employee</th><th>Project</th><th>Object ID</th><th>Date</th><th style='text-align:right;'>Amount (₹)</th>");
            sb.AppendLine("</tr></thead><tbody>");
            
            decimal total = 0;
            foreach (var r in records)
            {
                total += r.BillingAmount;
                sb.AppendLine($"<tr><td>{r.EmployeeName}</td><td>{r.ProjectName}</td><td>{r.ObjectId}</td><td>{r.SubmissionDate:dd/MM/yyyy}</td><td style='text-align:right;'>{r.BillingAmount:N2}</td></tr>");
            }
            
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("<div class='total-section'>");
            sb.AppendLine($"<span class='total-label'>GRAND TOTAL</span>");
            sb.AppendLine($"<span class='total-amount'>₹{total:N2}</span>");
            sb.AppendLine("</div>");
            
            sb.AppendLine("<div class='signature-section'>");
            sb.AppendLine("<div class='signature-box'>Verified By (Admin)</div>");
            sb.AppendLine("<div class='signature-box'>Employee Signature</div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='footer'>This is a system-generated document and does not require a physical stamp.</div>");
            sb.AppendLine("</body></html>");
            
            File.WriteAllText(filePath, sb.ToString());
        }

        public static void ExportAttendanceToHtml(IEnumerable<Attendance> records, string filePath)
        {
            var sb = new StringBuilder();
            sb.Append(GetHtmlHeader("Attendance Record"));
            
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>Employee</th><th>Date</th><th>Status</th><th>Clock In</th><th>Clock Out</th><th>Hours</th>");
            sb.AppendLine("</tr></thead><tbody>");
            
            foreach (var r in records)
            {
                sb.AppendLine($"<tr><td>{r.EmployeeName}</td><td>{r.Date:dd/MM/yyyy}</td><td>{r.Status}</td><td>{r.ClockInTime ?? "-"}</td><td>{r.ClockOutTime ?? "-"}</td><td>{r.HoursWorked:F1}</td></tr>");
            }
            
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("<div class='signature-section'><div class='signature-box'>HR Manager Signature</div></div>");
            sb.AppendLine("</body></html>");
            
            File.WriteAllText(filePath, sb.ToString());
        }

        public static void ExportWorkReportsToHtml(IEnumerable<WorkReport> reports, string filePath, string title = "Work Progress Report")
        {
            var sb = new StringBuilder();
            sb.Append(GetHtmlHeader(title));
            
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>ID</th><th>Employee</th><th>Project</th><th>Object ID</th><th>Date</th>");
            sb.AppendLine("</tr></thead><tbody>");
            
            foreach (var r in reports)
            {
                sb.AppendLine($"<tr><td>{r.Id}</td><td>{r.EmployeeName}</td><td>{r.ProjectName}</td><td>{r.ObjectId}</td><td>{r.SubmissionDate:dd/MM/yyyy}</td></tr>");
            }
            
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("<div class='footer'>Confidential Enterprise Document</div>");
            sb.AppendLine("</body></html>");
            
            File.WriteAllText(filePath, sb.ToString());
        }
    }
}
