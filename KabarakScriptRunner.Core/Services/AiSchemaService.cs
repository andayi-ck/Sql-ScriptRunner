using System.Data;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;

namespace KabarakScriptRunner.Core.Services
{
    public static class AiSchemaService
    {
        private static string? _cachedSchema;

        public static async Task<string> GetSchemaAsync(string connectionString)
        {
            if (!string.IsNullOrEmpty(_cachedSchema)) return _cachedSchema;

            using var connection = new SqlConnection(connectionString);
            // We only want to give the AI the 'uni' schema (read-only reporting data) to keep it safe and focused.
            const string sql = @"
                SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = 'uni' 
                ORDER BY TABLE_NAME, ORDINAL_POSITION";

            var rows = await connection.QueryAsync(sql);
            var sb = new StringBuilder();
            string currentTable = "";

            foreach (var row in rows)
            {
                var table = $"{row.TABLE_SCHEMA}.{row.TABLE_NAME}";
                if (table != currentTable)
                {
                    sb.AppendLine($"\nTable: {table}");
                    currentTable = table;
                }
                sb.AppendLine($"  - {row.COLUMN_NAME} ({row.DATA_TYPE})");
            }

            _cachedSchema = sb.ToString();
            return _cachedSchema;
        }
    }
}