using System.Data;
using System.IO;
using KabarakScriptRunner.Core.Services;
using Xunit;

namespace KabarakScriptRunner.Tests
{
    public class ExcelExportServiceTests
    {
        private DataTable BuildSampleTable()
        {
            var table = new DataTable("Test");
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Amount", typeof(decimal));
            table.Rows.Add(1, "Alice", 1234.56m);
            table.Rows.Add(2, "Bob", 7890.12m);
            return table;
        }

        [Fact]
        public void ExportToExcel_CreatesFile()
        {
            var outputFolder = Path.Combine(Path.GetTempPath(), "KabarakTest_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var table = BuildSampleTable();
                var filePath = ExcelExportService.ExportToExcel(table, outputFolder, "Test Report");

                Assert.True(File.Exists(filePath));
                Assert.EndsWith(".xlsx", filePath);
            }
            finally
            {
                if (Directory.Exists(outputFolder))
                    Directory.Delete(outputFolder, true);
            }
        }

        [Fact]
        public void ExportToExcel_FilenameIncludesScriptName()
        {
            var outputFolder = Path.Combine(Path.GetTempPath(), "KabarakTest_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var table = BuildSampleTable();
                var filePath = ExcelExportService.ExportToExcel(table, outputFolder, "Fees Report");
                var fileName = Path.GetFileName(filePath);

                Assert.Contains("Fees Report", fileName);
            }
            finally
            {
                if (Directory.Exists(outputFolder))
                    Directory.Delete(outputFolder, true);
            }
        }
    }
}