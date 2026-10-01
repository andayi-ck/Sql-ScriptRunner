using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace KabarakScriptRunner.Core.Services
{
    public static class ScriptExecutorService
    {
        /// <summary>
        /// Executes a parameterized SELECT statement and returns the result as a DataTable.
        /// Uses a 5-minute command timeout. Supports cooperative cancellation.
        /// </summary>
        public static async Task<DataTable> ExecuteScriptAsync(
            string connectionString,
            string sqlText,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sqlText))
                throw new ArgumentException("SQL text is empty.", nameof(sqlText));

            using var connection = new SqlConnection(connectionString);

            var parameters = new DynamicParameters();

            // Only pass parameters if the script actually references them
            if (sqlText.Contains("@DateFrom", StringComparison.OrdinalIgnoreCase))
                parameters.Add("@DateFrom", dateFrom, DbType.Date);

            if (sqlText.Contains("@DateTo", StringComparison.OrdinalIgnoreCase))
                parameters.Add("@DateTo", dateTo, DbType.Date);

            var table = new DataTable();

            using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    sqlText,
                    parameters,
                    commandTimeout: 300,
                    cancellationToken: cancellationToken));

            table.Load(reader);
            return table;
        }
    }
}