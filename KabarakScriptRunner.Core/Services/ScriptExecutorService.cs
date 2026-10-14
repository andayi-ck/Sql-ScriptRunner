using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Polly;

namespace KabarakScriptRunner.Core.Services
{
    public static class ScriptExecutorService
    {
        /// <summary>
        /// Executes a parameterized SELECT statement and returns the result as a DataTable.
        /// Uses a 5-minute command timeout. Supports cooperative cancellation.
        /// Retries up to 2 times on transient connection errors (2s, 4s delays).
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

            // Safety: refuse to execute any script that fails validation.
            // This is the last line of defense against bad SQL.
            ScriptValidator.ValidateOrThrow(sqlText);


            // Retry policy: 2 retries with linear backoff (2s, 4s).
            // Kept short because each query already has a 5-minute timeout.
            var policy = Policy
                .Handle<SqlException>(ex => IsTransient(ex))
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    retryCount: 2,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(attempt * 2),
                    onRetry: (exception, delay, attempt, _) =>
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ScriptExecutor] Retry {attempt} after {delay.TotalSeconds}s. Reason: {exception.Message}");
                    });

            return await policy.ExecuteAsync(async () =>
            {
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
            });
        }

        /// <summary>
        /// Common SQL Server error numbers that represent transient failures.
        /// </summary>
        private static bool IsTransient(SqlException ex)
        {
            int[] transientErrors = { -2, 20, 64, 233, 10053, 10054, 10060, 1205, 4060, 10928, 10929, 40197, 40501, 40613, 49918, 49919, 49920 };
            return Array.IndexOf(transientErrors, ex.Number) >= 0;
        }
    }
}