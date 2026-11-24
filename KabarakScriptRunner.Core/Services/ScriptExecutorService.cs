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

            // CN-08: Retry loop is now cancellation-aware. We no longer rely on
            // Polly's WaitAndRetryAsync (whose Thread.Sleep backoff ignores the
            // token). Instead we retry manually with Task.Delay(..., token) so a
            // Cancel during backoff surfaces immediately instead of after the
            // 2s + 4s sleeps finish.
            const int maxAttempts = 3; // 1 initial + 2 retries, matches prior retryCount: 2
            var exceptions = new List<Exception>();

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await ExecuteOnceAsync(connectionString, sqlText, dateFrom, dateTo, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // User cancellation — never retry.
                    throw;
                }
                catch (Exception ex) when (IsRetryable(ex))
                {
                    exceptions.Add(ex);

                    if (attempt == maxAttempts)
                        break;

                    var delay = TimeSpan.FromSeconds(attempt * 2); // 2s, 4s
                    System.Diagnostics.Debug.WriteLine(
                        $"[ScriptExecutor] Retry {attempt} after {delay.TotalSeconds}s. Reason: {ex.Message}");

                    // CN-08: Cancellation-aware backoff. Throws OperationCanceledException
                    // immediately if the user cancels mid-sleep.
                    await Task.Delay(delay, cancellationToken);
                }
            }

            // All attempts exhausted — rethrow the last failure with context.
            throw new InvalidOperationException(
                $"Script execution failed after {maxAttempts} attempt(s).", exceptions[^1]);
        }

        private static async Task<DataTable> ExecuteOnceAsync(
            string connectionString,
            string sqlText,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken)
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
        }

        private static bool IsRetryable(Exception ex)
        {
            return ex switch
            {
                SqlException sql => IsTransient(sql),
                TimeoutException => true,
                _ => false
            };
        }

        private static bool IsTransient(SqlException ex)
        {
            int[] transientErrors = { -2, 20, 64, 233, 10053, 10054, 10060, 1205, 4060, 10928, 10929, 40197, 40501, 40613, 49918, 49919, 49920 };
            return Array.IndexOf(transientErrors, ex.Number) >= 0;
        }
    }
}