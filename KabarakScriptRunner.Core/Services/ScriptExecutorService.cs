using System;
using System.Collections.Generic;
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
            // Using the fully qualified name to avoid any namespace issues.
            var (isValid, errors) = KabarakScriptRunner.Core.Services.ScriptValidator.Validate(sqlText);
            if (!isValid)
            {
                throw new InvalidOperationException(
                    "Script validation failed:\n" + string.Join("\n", errors));
            }

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

        private static bool IsTransient(SqlException ex)
        {
            int[] transientErrors = { -2, 20, 64, 233, 10053, 10054, 10060, 1205, 4060, 10928, 10929, 40197, 40501, 40613, 49918, 49919, 49920 };
            return Array.IndexOf(transientErrors, ex.Number) >= 0;
        }
    }
}