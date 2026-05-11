using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EnterpriseWorkReport.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;

namespace EnterpriseWorkReport.Services
{
    public class ReportingService
    {
        static ReportingService()
        {
            // QuestPDF Community License
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public string ExportWorkReportSummaryPdf(IEnumerable<WorkReport> reports, string filePath)
        {
            var company = new CompanyService().GetSettings();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                    // Header
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(company.CompanyName).FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"{company.CompanyAddress} | {company.CompanyPhone}").FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("WORK REPORT SUMMARY").FontSize(16).SemiBold();
                            col.Item().Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                    });

                    // Table Content
                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(80); // Date
                            columns.RelativeColumn();   // Project
                            columns.RelativeColumn();   // Employee
                            columns.ConstantColumn(100); // Object ID
                            columns.ConstantColumn(80); // Amount
                        });

                        // Header Row
                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Date");
                            header.Cell().Element(CellStyle).Text("Project");
                            header.Cell().Element(CellStyle).Text("Employee");
                            header.Cell().Element(CellStyle).Text("Object ID");
                            header.Cell().Element(CellStyle).AlignRight().Text("Amount");

                            static IContainer CellStyle(IContainer container)
                            {
                                return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                            }
                        });

                        // Content Rows
                        foreach (var report in reports)
                        {
                            table.Cell().Element(ItemStyle).Text(report.SubmissionDate.ToString("dd/MM/yyyy"));
                            table.Cell().Element(ItemStyle).Text(report.ProjectName);
                            table.Cell().Element(ItemStyle).Text(report.EmployeeName);
                            table.Cell().Element(ItemStyle).Text(report.ObjectId);
                            table.Cell().Element(ItemStyle).AlignRight().Text($"{company.CurrencySymbol}{report.BillingAmount:F2}");

                            static IContainer ItemStyle(IContainer container)
                            {
                                return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten3);
                            }
                        }
                        
                        // Footer Sum
                        table.Footer(footer =>
                        {
                             footer.Cell().ColumnSpan(4).AlignRight().PaddingVertical(10).Text("Grand Total:").SemiBold();
                             footer.Cell().AlignRight().PaddingVertical(10).Text($"{company.CurrencySymbol}{reports.Sum(r => r.BillingAmount):F2}").SemiBold().FontColor(Colors.Blue.Medium);
                        });
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                    });
                });
            }).GeneratePdf(filePath);

            return filePath;
        }

        public string ExportWorkReportDetailedPdf(IEnumerable<WorkReport> reports, string filePath)
        {
            var company = new CompanyService().GetSettings();

            Document.Create(container =>
            {
                foreach (var report in reports)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Verdana));

                        // Professional Header
                        page.Header().BorderBottom(2).BorderColor(Colors.Blue.Darken2).PaddingBottom(10).Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text(company.CompanyName).FontSize(24).ExtraBold().FontColor(Colors.Blue.Darken2);
                                col.Item().Text(company.CompanyAddress).FontSize(10);
                                col.Item().Text($"Tax ID: {company.TaxId} | Email: {company.CompanyEmail}").FontSize(9).FontColor(Colors.Grey.Medium);
                            });

                            row.ConstantItem(100).AlignRight().Column(col =>
                            {
                                col.Item().Text("REPORT").FontSize(22).ExtraBold().FontColor(Colors.Grey.Lighten2);
                                col.Item().Text($"Ref: WR-{report.Id:D5}").FontSize(10).SemiBold();
                            });
                        });

                        // Main info
                        page.Content().PaddingVertical(20).Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c => {
                                    c.Item().Text("Employee Info").SemiBold().Underline();
                                    c.Item().Text(report.EmployeeName).FontSize(14).SemiBold();
                                    c.Item().Text("Authorized Professional").FontSize(9).Italic();
                                });
                                row.RelativeItem().AlignRight().Column(c => {
                                    c.Item().Text("Project Details").SemiBold().Underline();
                                    c.Item().Text(report.ProjectName).FontSize(14).SemiBold();
                                    c.Item().Text($"Date: {report.SubmissionDate:dd MMMM yyyy}").FontSize(11);
                                });
                            });

                            col.Item().PaddingTop(30).Background(Colors.Grey.Lighten4).Padding(15).Row(row =>
                            {
                                row.RelativeItem().Text("Item Description").SemiBold();
                                row.ConstantItem(150).AlignRight().Text("Value").SemiBold();
                            });

                            // Report ID and Object ID
                            col.Item().PaddingHorizontal(15).PaddingVertical(10).Row(row => {
                                row.RelativeItem().Text("Object Identification");
                                row.ConstantItem(150).AlignRight().Text(report.ObjectId).SemiBold();
                            });

                            // Dynamic Fields
                            foreach (var item in report.Items)
                            {
                                col.Item().PaddingHorizontal(15).PaddingVertical(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Row(row => {
                                    row.RelativeItem().Text(item.FieldLabel);
                                    row.ConstantItem(150).AlignRight().Text(item.Value);
                                });
                            }

                            // Notes
                            if (!string.IsNullOrEmpty(report.AdminNote))
                            {
                                col.Item().PaddingTop(20).Column(c => {
                                    c.Item().Text("Administrative Notes:").SemiBold().FontSize(9);
                                    c.Item().Text(report.AdminNote).Italic().FontColor(Colors.Grey.Darken1);
                                });
                            }

                            // Bottom Billing Total
                            col.Item().AlignRight().PaddingTop(40).BorderTop(1).PaddingTop(10).Row(row => {
                                row.ConstantItem(200).AlignRight().Column(c => {
                                    c.Item().Text("Total Billing Amount").FontSize(10).SemiBold();
                                    c.Item().Text($"{company.CurrencySymbol}{report.BillingAmount:F2}").FontSize(22).ExtraBold().FontColor(Colors.Blue.Medium);
                                });
                            });
                            
                            // Signature area
                            col.Item().PaddingTop(60).Row(row => {
                                row.RelativeItem().Column(c => {
                                    c.Item().PaddingTop(10).BorderTop(1).AlignCenter().Text("Employee Signature").FontSize(8);
                                });
                                row.ConstantItem(100);
                                row.RelativeItem().Column(c => {
                                    c.Item().PaddingTop(10).BorderTop(1).AlignCenter().Text("Authorized Signatory").FontSize(8);
                                });
                            });
                        });

                        page.Footer().AlignRight().Text(x =>
                        {
                            x.Span("WR-");
                            x.Span(report.Id.ToString("D5"));
                            x.Span(" | Page ");
                            x.CurrentPageNumber();
                        });
                    });
                }
            }).GeneratePdf(filePath);

            return filePath;
        }
    }
}
