using System.Data;
using ClosedXML.Excel;

namespace KabarakScriptRunner.Core.Services
{
    public static class ExcelExportService
    {
        /// <summary>
        /// Writes a DataTable to an .xlsx file.
        /// Returns the full path of the created file.
        /// </summary>
        public static string ExportToExcel(DataTable table, string outputFolder, string scriptName)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            // Ensure output folder exists
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            // Build the filename: ScriptName_yyyyMMdd_HHmm.xlsx
            var safeName = MakeSafeFileName(scriptName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var fileName = $"{safeName}_{timestamp}.xlsx";
            var fullPath = Path.Combine(outputFolder, fileName);

            // Create the workbook
            using var workbook = new XLWorkbook();
            var sheetName = MakeSafeSheetName(scriptName);
            var worksheet = workbook.Worksheets.Add(sheetName);

            // Header row (row 1)
            for (int col = 0; col < table.Columns.Count; col++)
            {
                var cell = worksheet.Cell(1, col + 1);
                cell.Value = table.Columns[col].ColumnName;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            // Data rows (starting from row 2)
            for (int row = 0; row < table.Rows.Count; row++)
            {
                for (int col = 0; col < table.Columns.Count; col++)
                {
                    var value = table.Rows[row][col];
                    var cell = worksheet.Cell(row + 2, col + 1);

                    if (value == null || value == DBNull.Value)
                    {
                        cell.Value = "";
                    }
                    else if (value is DateTime dt)
                    {
                        cell.Value = dt;
                        cell.Style.DateFormat.Format = "yyyy-mm-dd";
                    }
                    else if (value is decimal or double or float)
                    {
                        cell.Value = Convert.ToDouble(value);
                    }
                    else if (value is int or long or short or byte)
                    {
                        cell.Value = Convert.ToInt64(value);
                    }
                    else if (value is bool b)
                    {
                        cell.Value = b;
                    }
                    else
                    {
                        cell.Value = value.ToString();
                    }
                }
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Add a title/footer row? Optional, skip for now.

            // Freeze the header row
            worksheet.SheetView.FreezeRows(1);

            workbook.SaveAs(fullPath);
            return fullPath;
        }

        private static string MakeSafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (safe.Length > 80) safe = safe.Substring(0, 80);
            return string.IsNullOrWhiteSpace(safe) ? "Export" : safe;
        }

        private static string MakeSafeSheetName(string name)
        {
            // Excel sheet names: max 31 chars, cannot contain \ / ? * [ ] :
            var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
            var safe = new string(name.Where(c => !invalid.Contains(c)).ToArray());
            if (safe.Length > 31) safe = safe.Substring(0, 31);
            return string.IsNullOrWhiteSpace(safe) ? "Export" : safe;
        }
    }
}