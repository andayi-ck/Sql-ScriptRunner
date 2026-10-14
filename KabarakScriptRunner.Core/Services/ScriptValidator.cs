using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KabarakScriptRunner.Core.Services
{
    /// <summary>
    /// Validates that a SQL script is safe to store and run.
    /// Rules: only SELECT statements; no DDL/DML; no dangerous keywords;
    /// no multiple statements; only allow-listed parameters.
    /// </summary>
    public static class ScriptValidator
    {
        /// <summary>
        /// Keywords that are forbidden anywhere in the script.
        /// Word-boundary matched so we don't false-positive on identifiers
        /// like "CreatedDate" containing "create".
        /// </summary>
        private static readonly string[] ForbiddenKeywords =
        {
            // DML
            "INSERT", "UPDATE", "DELETE", "MERGE", "TRUNCATE",
            // DDL
            "CREATE", "ALTER", "DROP",
            // Permissions & execution
            "GRANT", "REVOKE", "DENY", "EXEC", "EXECUTE",
            // Server-level
            "SHUTDOWN", "BACKUP", "RESTORE", "BULK",
            // Stored proc / function creation
            "PROCEDURE", "FUNCTION", "TRIGGER", "VIEW",
            // Transaction control (scripts should be read-only)
            "COMMIT", "ROLLBACK", "BEGIN TRANSACTION", "BEGIN TRAN",
            // Temp tables
            "INTO #",  "SELECT INTO",
            // Cursor / waiting
            "WAITFOR",
            // Dynamic SQL
            "sp_executesql", "xp_cmdshell", "OPENROWSET", "OPENDATASOURCE",
            "OPENQUERY", "BULK INSERT"
        };

        /// <summary>
        /// Parameters we allow scripts to reference.
        /// </summary>
        private static readonly string[] AllowedParameters = { "@DateFrom", "@DateTo" };

        public class ValidationResult
        {
            public bool IsValid => Errors.Count == 0;
            public List<string> Errors { get; } = new();
        }

        /// <summary>
        /// Validates the SQL text. Returns a result with a list of errors
        /// if the script is unsafe to save or run.
        /// </summary>
        public static ValidationResult Validate(string sqlText)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(sqlText))
            {
                result.Errors.Add("The script is empty.");
                return result;
            }

            var trimmed = sqlText.Trim();

            // 1. Must start with SELECT (or WITH ... SELECT for CTEs)
            if (!StartsWithSelectOrCte(trimmed))
            {
                result.Errors.Add(
                    "The script must start with SELECT (or WITH ... SELECT for CTEs). " +
                    "Only read-only queries are allowed.");
            }

            // 2. Strip comments and strings so keyword checks don't false-positive
            var stripped = StripCommentsAndStrings(trimmed);

            // 3. Check forbidden keywords
            var upper = stripped.ToUpperInvariant();
            foreach (var keyword in ForbiddenKeywords)
            {
                var pattern = keyword.EndsWith(" ")
                    ? Regex.Escape(keyword)
                    : $@"\b{Regex.Escape(keyword)}\b";

                if (Regex.IsMatch(upper, pattern))
                {
                    result.Errors.Add(
                        $"Forbidden keyword detected: '{keyword.Trim()}'. " +
                        "Only SELECT queries are permitted.");
                }
            }

            // 4. Check for multiple statements (semicolon followed by more SQL)
            if (HasMultipleStatements(stripped))
            {
                result.Errors.Add(
                    "Multiple statements detected. The script must contain only one SELECT statement.");
            }

            // 5. Check that referenced parameters are allowed
            var referencedParams = ExtractParameters(stripped);
            foreach (var param in referencedParams)
            {
                if (!AllowedParameters.Contains(param, StringComparer.OrdinalIgnoreCase))
                {
                    result.Errors.Add(
                        $"Unsupported parameter '{param}'. " +
                        $"Only {string.Join(" and ", AllowedParameters)} are allowed.");
                }
            }

            return result;
        }

        /// <summary>
        /// Convenience: throws an exception if the script is not valid.
        /// </summary>
        public static void ValidateOrThrow(string sqlText)
        {
            var result = Validate(sqlText);
            if (!result.IsValid)
            {
                throw new InvalidOperationException(
                    "Script validation failed:\n" + string.Join("\n", result.Errors));
            }
        }

        // ============================================
        // HELPERS
        // ============================================

        private static bool StartsWithSelectOrCte(string sql)
        {
            var upper = sql.TrimStart().ToUpperInvariant();
            return upper.StartsWith("SELECT") || upper.StartsWith("WITH");
        }

        /// <summary>
        /// Removes single-line comments, block comments, and string literals.
        /// This prevents keyword checks from triggering on harmless content.
        /// </summary>
        private static string StripCommentsAndStrings(string sql)
        {
            // Remove block comments /* ... */
            sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);

            // Remove single-line comments -- ...
            sql = Regex.Replace(sql, @"--[^\r\n]*", " ");

            // Replace string literals '...' with a placeholder (handles escaped '')
            sql = Regex.Replace(sql, @"'(?:[^']|'')*'", "''");

            return sql;
        }

        /// <summary>
        /// Detects multiple SQL statements by looking for a semicolon
        /// followed by any non-whitespace, non-comment content.
        /// </summary>
        private static bool HasMultipleStatements(string sql)
        {
            // A single trailing semicolon is fine; anything after is not.
            var trimmed = sql.TrimEnd();
            if (trimmed.EndsWith(";"))
                trimmed = trimmed.Substring(0, trimmed.Length - 1);

            // Now check if there's still a semicolon
            int idx = trimmed.IndexOf(';');
            if (idx < 0) return false;

            // Is there non-whitespace after the semicolon?
            var after = trimmed.Substring(idx + 1).Trim();
            return after.Length > 0;
        }

        /// <summary>
        /// Extracts all SQL parameter references (starting with @).
        /// </summary>
        private static List<string> ExtractParameters(string sql)
        {
            var matches = Regex.Matches(sql, @"@[A-Za-z_][A-Za-z0-9_]*");
            return matches
                .Cast<Match>()
                .Select(m => m.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}