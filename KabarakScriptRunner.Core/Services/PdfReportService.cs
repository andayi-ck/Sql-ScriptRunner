using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KabarakScriptRunner.Core.Services
{
    /// <summary>
    /// Professional PDF report generator using QuestPDF.
    /// Supports data reports (from a DataTable) and dashboard summary reports.
    /// </summary>
    public static class PdfReportService
    {
        // Branding (all as QuestPDF Color values so `var bg = alt ? X : Y` works cleanly)
        private static readonly Color PrimaryColor = Color.FromHex("#1565C0");
        private static readonly Color AccentColor = Color.FromHex("#4FC3F7");
        private static readonly Color HeaderBg = Color.FromHex("#E3F2FD");
        private static readonly Color RowAltBg = Color.FromHex("#F5F9FF");
        private static readonly Color BorderColor = Color.FromHex("#CFD8DC");
        private static readonly Color WhiteBg = Colors.White;

        // ============================================
        // 1. DATA REPORT
        // ============================================
        public static string GenerateDataReport(
            DataTable table,
            string outputFolder,
            string reportTitle,
            string? subtitle = null,
            string? generatedBy = null,
            IEnumerable<string>? summaryMetrics = null)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var safeTitle = MakeSafeFileName(reportTitle);
            var fileName = $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            var fullPath = Path.Combine(outputFolder, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(24);
                    page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Lato"));

                    page.Header().Element(header => ComposeHeader(header, reportTitle, subtitle, generatedBy));

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        if (summaryMetrics != null && summaryMetrics.Any())
                        {
                            col.Item().PaddingBottom(8).Element(container =>
                            {
                                container.Background(HeaderBg).Padding(8).Border(1).BorderColor(BorderColor)
                                    .Column(inner =>
                                    {
                                        inner.Item().Text("Summary").Bold().FontColor(PrimaryColor);
                                        foreach (var metric in summaryMetrics)
                                            inner.Item().PaddingTop(2).Text(metric);
                                    });
                            });
                        }

                        col.Item().Element(e => ComposeDataTable(e, table));
                    });

                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf(fullPath);

            return fullPath;
        }

        // ============================================
        // 2. DASHBOARD SUMMARY REPORT
        // ============================================
        public static string GenerateDashboardReport(
            string outputFolder,
            DashboardSummaryData data)
        {
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var fileName = $"Dashboard_Summary_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            var fullPath = Path.Combine(outputFolder, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Portrait());
                    page.Margin(30);
                    page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(h =>
                        ComposeHeader(h, "Kabarak Script Runner — Dashboard Summary",
                            $"Generated {DateTime.Now:yyyy-MM-dd HH:mm}", null));

                    page.Content().PaddingTop(14).Column(col =>
                    {
                        col.Spacing(14);

                        col.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(e => ComposeKpiCard(e, "Active Students", data.ActiveStudents.ToString("N0")));
                            row.RelativeItem().Element(e => ComposeKpiCard(e, "Active Staff", data.ActiveStaff.ToString("N0")));
                            row.RelativeItem().Element(e => ComposeKpiCard(e, "Active Scripts", data.ActiveScripts.ToString("N0")));
                        });

                        col.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(e => ComposeKpiCard(e, "Fees (Last 30 days)", "KES " + data.FeesLast30Days.ToString("N0")));
                            row.RelativeItem().Element(e => ComposeKpiCard(e, "Runs (Last 7 days)", data.RunsLast7Days.ToString("N0")));
                            row.RelativeItem().Element(e => ComposeKpiCard(e, "Total Scripts", data.TotalScripts.ToString("N0")));
                        });

                        if (data.FeesByMonth.Any())
                        {
                            col.Item().PaddingTop(6).Text("Fees Collected — Last 12 Months")
                                .FontSize(12).Bold().FontColor(PrimaryColor);

                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(3);
                                });

                                t.Header(h =>
                                {
                                    h.Cell().Background(HeaderBg).Padding(6).Text("Month").Bold();
                                    h.Cell().Background(HeaderBg).Padding(6).AlignRight().Text("Amount (KES)").Bold();
                                });

                                bool alt = false;
                                foreach (var item in data.FeesByMonth)
                                {
                                    Color bg = alt ? RowAltBg : WhiteBg;
                                    t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(BorderColor).Padding(5).Text(item.Period);
                                    t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(BorderColor).Padding(5).AlignRight().Text(item.Total.ToString("N0"));
                                    alt = !alt;
                                }
                            });
                        }

                        if (data.StudentsByYear.Any())
                        {
                            col.Item().PaddingTop(6).Text("Students Admitted Per Year")
                                .FontSize(12).Bold().FontColor(PrimaryColor);

                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(3);
                                });

                                t.Header(h =>
                                {
                                    h.Cell().Background(HeaderBg).Padding(6).Text("Year").Bold();
                                    h.Cell().Background(HeaderBg).Padding(6).AlignRight().Text("Students Admitted").Bold();
                                });

                                bool alt = false;
                                foreach (var item in data.StudentsByYear)
                                {
                                    Color bg = alt ? RowAltBg : WhiteBg;
                                    t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(BorderColor).Padding(5).Text(item.Year.ToString());
                                    t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(BorderColor).Padding(5).AlignRight().Text(item.Total.ToString("N0"));
                                    alt = !alt;
                                }
                            });
                        }

                        if (data.RecentActivity.Any())
                        {
                            col.Item().PaddingTop(6).Text("Recent Activity")
                                .FontSize(12).Bold().FontColor(PrimaryColor);

                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(3);
                                    c.RelativeColumn(5);
                                });

                                t.Header(h =>
                                {
                                    h.Cell().Background(HeaderBg).Padding(5).Text("Time").Bold();
                                    h.Cell().Background(HeaderBg).Padding(5).Text("User").Bold();
                                    h.Cell().Background(HeaderBg).Padding(5).Text("Action").Bold();
                                    h.Cell().Background(HeaderBg).Padding(5).Text("Details").Bold();
                                });

                                bool alt = false;
                                foreach (var item in data.RecentActivity)
                                {
                                    Color bg = alt ? RowAltBg : WhiteBg;
                                    t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(BorderColor).Padding(4).Text(item.Timestamp.ToString("yyyy-MM-dd HH:mm"));
                                    t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(BorderColor).Padding(4).Text(item.UserId);
                                    t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(BorderColor).Padding(4).Text(item.ActionType);
                                    t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(BorderColor).Padding(4).Text(item.Details ?? "");
                                    alt = !alt;
                                }
                            });
                        }
                    });

                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf(fullPath);

            return fullPath;
        }

        // ============================================
        // SHARED COMPOSERS
        // ============================================
        private static void ComposeHeader(IContainer container, string title, string? subtitle, string? generatedBy)
        {
            container
                .BorderBottom(2).BorderColor(PrimaryColor)
                .PaddingBottom(8)
                .Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("KABARAK UNIVERSITY")
                            .FontSize(14).Bold().FontColor(PrimaryColor);
                        col.Item().Text(title).FontSize(12).SemiBold();
                        if (!string.IsNullOrWhiteSpace(subtitle))
                            col.Item().Text(subtitle).FontSize(10).FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(200).AlignRight().Column(col =>
                    {
                        col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(generatedBy))
                            col.Item().Text($"By: {generatedBy}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeFooter(IContainer container)
        {
            container
                .BorderTop(1).BorderColor(BorderColor)
                .PaddingTop(6)
                .Row(row =>
                {
                    row.RelativeItem().Text("Kabarak Script Runner — Confidential")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);

                    row.ConstantItem(140).AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(8);
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(" of ").FontSize(8);
                        text.TotalPages().FontSize(8);
                    });
                });
        }

        private static void ComposeDataTable(IContainer container, DataTable table)
        {
            container.Table(t =>
            {
                t.ColumnsDefinition(cols =>
                {
                    for (int i = 0; i < table.Columns.Count; i++)
                        cols.RelativeColumn();
                });

                t.Header(header =>
                {
                    foreach (DataColumn column in table.Columns)
                    {
                        header.Cell()
                            .Background(HeaderBg)
                            .BorderBottom(1).BorderColor(PrimaryColor)
                            .Padding(5)
                            .Text(column.ColumnName)
                            .Bold()
                            .FontColor(PrimaryColor);
                    }
                });

                int rowIndex = 0;
                foreach (DataRow row in table.Rows)
                {
                    rowIndex++;
                    Color bg = rowIndex % 2 == 0 ? RowAltBg : WhiteBg;

                    foreach (var value in row.ItemArray)
                    {
                        var text = FormatCellValue(value);
                        var numeric = value is decimal or double or float or int or long or short;

                        var cell = t.Cell()
                            .Background(bg)
                            .BorderBottom(0.5f).BorderColor(BorderColor)
                            .Padding(4);

                        if (numeric)
                            cell.AlignRight().Text(text);
                        else
                            cell.Text(text);
                    }
                }
            });
        }

        private static void ComposeKpiCard(IContainer container, string label, string value)
        {
            container
                .Background(HeaderBg)
                .Border(1).BorderColor(BorderColor)
                .Padding(12)
                .Column(col =>
                {
                    col.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(4).Text(value).FontSize(20).Bold().FontColor(PrimaryColor);
                });
        }

        private static string FormatCellValue(object? value)
        {
            if (value == null || value == DBNull.Value) return "";
            if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm");
            if (value is decimal dec) return dec.ToString("N2");
            if (value is double dbl) return dbl.ToString("N2");
            if (value is float flt) return flt.ToString("N2");
            if (value is int or long or short) return Convert.ToInt64(value).ToString("N0");
            return value.ToString() ?? "";
        }

        private static string MakeSafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (safe.Length > 60) safe = safe.Substring(0, 60);
            return string.IsNullOrWhiteSpace(safe) ? "Report" : safe;
        }
    }

    // ============================================
    // DTO for Dashboard PDF
    // ============================================
    public class DashboardSummaryData
    {
        public int ActiveStudents { get; set; }
        public int ActiveStaff { get; set; }
        public int ActiveScripts { get; set; }
        public int TotalScripts { get; set; }
        public decimal FeesLast30Days { get; set; }
        public int RunsLast7Days { get; set; }
        public List<MonthTotal> FeesByMonth { get; set; } = new();
        public List<YearTotal> StudentsByYear { get; set; } = new();
        public List<ActivityRow> RecentActivity { get; set; } = new();
    }

    public class MonthTotal { public string Period { get; set; } = ""; public decimal Total { get; set; } }
    public class YearTotal { public int Year { get; set; } public int Total { get; set; } }
    public class ActivityRow
    {
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; } = "";
        public string ActionType { get; set; } = "";
        public string? Details { get; set; }
    }
}