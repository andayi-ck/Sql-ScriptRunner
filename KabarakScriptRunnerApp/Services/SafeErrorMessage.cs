using System;
using System.IO;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Serilog;

namespace KabarakScriptRunnerApp.Services
{
    /// <summary>
    /// EH-03: Single source of truth for turning an exception into a user-safe
    /// message. No exception detail, SQL text, connection string, file path or
    /// stack trace is ever returned. The full exception goes to Serilog; the
    /// caller gets a generic string plus a correlation ID support can grep for.
    /// </summary>
    public static class SafeErrorMessage
    {
        /// <summary>
        /// Returns a user-safe message for <paramref name="ex"/> and logs the full
        /// exception under <paramref name="context"/>. The returned string is safe
        /// to place in any MessageBox, label, toast, progress line, or audit row.
        /// </summary>
        public static string For(Exception ex, string context)
        {
            var correlationId = DateTime.Now.ToString("HH:mm:ss.fff");
            Log.Error(ex, "Error in {Context} (ref {CorrelationId})", context, correlationId);
            return $"{MapToSafeText(ex)} (ref {correlationId})";
        }

        /// <summary>
        /// Maps a known exception type to a generic, non-revealing sentence.
        /// Unknown types fall through to a generic message — never ex.Message.
        /// </summary>
        public static string MapToSafeText(Exception ex)
        {
            return ex switch
            {
                OperationCanceledException => "The operation was cancelled.",
                SqlException => "The database is unavailable or the query failed. Please try again.",
                DbException => "A database error occurred. Please try again.",
                TimeoutException => "The operation timed out. Please try again.",
                UnauthorizedAccessException => "Access was denied for this operation.",
                FileNotFoundException => "A required file could not be found.",
                DirectoryNotFoundException => "A required folder could not be found.",
                IOException => "A file operation failed. Please check the output folder and try again.",
                ArgumentException => "The request contained invalid input.",
                InvalidOperationException => "The operation could not be completed in the current state.",
                _ => "An unexpected error occurred. Please try again or contact your administrator."
            };
        }

        /// <summary>
        /// EH-04: Safe category for persisted failure reasons (app.ScriptRuns.ErrorMessage
        /// and admin-audit details). Short, stable, non-revealing.
        /// </summary>
        public static string CategoryFor(Exception ex)
        {
            return ex switch
            {
                OperationCanceledException => "Cancelled",
                SqlException => "Database error",
                DbException => "Database error",
                TimeoutException => "Timeout",
                UnauthorizedAccessException => "Access denied",
                FileNotFoundException or DirectoryNotFoundException => "File not found",
                IOException => "File I/O error",
                ArgumentException => "Invalid input",
                InvalidOperationException => "Validation/state error",
                _ => "Unexpected error"
            };
        }

        /// <summary>
        /// EH-04: Safe persisted failure reason — category + correlation ID so support
        /// can match the audit row to the Serilog entry without leaking detail.
        /// </summary>
        public static string PersistedReasonFor(Exception ex, string context)
        {
            var correlationId = DateTime.Now.ToString("HH:mm:ss.fff");
            Log.Error(ex, "Error in {Context} (ref {CorrelationId})", context, correlationId);
            return $"{CategoryFor(ex)} (ref {correlationId})";
        }
    }
}