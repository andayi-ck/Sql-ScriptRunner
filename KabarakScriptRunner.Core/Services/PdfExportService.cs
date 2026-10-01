using System.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KabarakScriptRunner.Core.Services
{
    public static class PdfExportService
    {
        public static string ExportDataTableToPdf(
            DataTable table,
            string outputFolder,
            string reportTitle,
            string? subtitle = null)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var safeTitle = MakeSafeFileName(reportTitle);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var fileName = $"{safeTitle}_{timestamp}.pdf";
            var fullPath = Path.Combine(outputFolder, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(t => t.FontSize(9));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(reportTitle).FontSize(14).Bold();
                        if (!string.IsNullOrWhiteSpace(subtitle))
                            col.Item().Text(subtitle).FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
                        col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    page.Content().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in table.Columns)
                                columns.RelativeColumn();
                        });

                        // Header
                        t.Header(h =>
                        {
                            foreach (DataColumn c in table.Columns)
                            {
                                h.Cell()
                                 .Background(Colors.Grey.Lighten2)
                                 .BorderBottom(1).BorderColor(Colors.Grey.Medium)
                                 .Padding(4)
                                 .Text(c.ColumnName).Bold().FontSize(9);
                            }
                        });

                        // Body
                        int rowIndex = 0;
                        foreach (DataRow row in table.Rows)
                        {
                            rowIndex++;
                            var bg = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                            foreach (var cell in row.ItemArray)
                            {
                                t.Cell()
                                 .Background(bg)
                                 .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                 .Padding(3)
                                 .Text(FormatCell(cell));
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ").FontSize(8);
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(" of ").FontSize(8);
                        text.TotalPages().FontSize(8);
                    });
                });
            }).GeneratePdf(fullPath);

            return fullPath;
        }

        private static string FormatCell(object? value)
        {
            if (value == null || value == DBNull.Value) return "";
            if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm");
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
}