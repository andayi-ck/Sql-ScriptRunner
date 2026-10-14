using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;

namespace KabarakScriptRunner.Core.Services
{
    public static class RetryPolicyFactory
    {
        /// <summary>
        /// A retry policy for SQL Server operations.
        /// Retries up to 3 times with exponential backoff (1s, 2s, 4s)
        /// when the error is a transient network/SQL issue.
        /// </summary>
        public static AsyncRetryPolicy CreateSqlRetryPolicy()
        {
            return Policy
                .Handle<SqlException>(IsTransient)
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                    onRetry: (exception, delay, attempt, _) =>
                    {
                        Console.WriteLine(
                            $"[RetryPolicy] SQL call failed. Attempt {attempt} will retry in {delay.TotalSeconds}s. Reason: {exception.Message}");
                    });
        }

        /// <summary>
        /// Determines whether a SqlException represents a transient failure
        /// that's worth retrying.
        /// </summary>
        private static bool IsTransient(SqlException ex)
        {
            // Common transient SQL Server error numbers:
            //  -2    Timeout expired
            //  20    The instance does not support encryption
            //  64    A connection was successfully established with the server,
            //        but then an error occurred
            //  233   Connection init error
            //  10053 Connection aborted by software
            //  10054 Connection reset by peer
            //  10060 Connection timed out
            //  1205  Deadlock victim
            //  4060  Cannot open database (may be transient on failover)
            //  10928 Resource ID limit
            //  10929 Resource ID limit
            //  40197 Service error
            //  40501 Service busy
            //  40613 Database unavailable
            //  49918 Too many operations on the same resource
            //  49919 Too many create/update operations in progress
            //  49920 Too many operations in progress
            int[] transientErrors = { -2, 20, 64, 233, 10053, 10054, 10060, 1205, 4060, 10928, 10929, 40197, 40501, 40613, 49918, 49919, 49920 };
            return Array.IndexOf(transientErrors, ex.Number) >= 0;
        }
    }
}