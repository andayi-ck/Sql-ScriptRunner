using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using KabarakScriptRunner.Data.Models;
using Microsoft.Data.SqlClient;
using Polly;

namespace KabarakScriptRunner.Data.Repositories
{
    public class ScriptRepository
    {
        private readonly DatabaseContext _context;

        public ScriptRepository(DatabaseContext context)
        {
            _context = context;
        }

        // ============================================
        // USERS
        // ============================================

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT u.UserId, u.DisplayName, u.PasswordHash, u.IsAdmin, u.IsActive,
                           u.DepartmentId, d.DepartmentName
                    FROM app.Users u
                    LEFT JOIN app.Departments d ON u.DepartmentId = d.DepartmentId
                    WHERE u.UserId = @UserId AND u.IsActive = 1";

                return await connection.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId });
            });
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT u.UserId, u.DisplayName, u.PasswordHash, u.IsAdmin, u.IsActive,
                           u.DepartmentId, d.DepartmentName
                    FROM app.Users u
                    LEFT JOIN app.Departments d ON u.DepartmentId = d.DepartmentId
                    ORDER BY u.DisplayName";

                return await connection.QueryAsync<User>(sql);
            });
        }

        public async Task UpdateUserPasswordAsync(string userId, string newPasswordHash)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = "UPDATE app.Users SET PasswordHash = @Hash WHERE UserId = @UserId";
                await connection.ExecuteAsync(sql, new { Hash = newPasswordHash, UserId = userId });
            });
        }

        // ============================================
        // DEPARTMENTS
        // ============================================

        public async Task<IEnumerable<Department>> GetDepartmentsAsync()
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = "SELECT DepartmentId, DepartmentName, IsActive FROM app.Departments WHERE IsActive = 1 ORDER BY DepartmentName";
                return await connection.QueryAsync<Department>(sql);
            });
        }

        // ============================================
        // SCRIPTS
        // ============================================

        public async Task<IEnumerable<Script>> GetScriptsAsync(int? departmentId = null, bool isAdmin = false)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT s.ScriptId, s.Name, s.Description, s.SqlText, s.Category,
                           s.NeedsDateFrom, s.NeedsDateTo, s.IsActive,
                           s.DepartmentId, d.DepartmentName
                    FROM app.Scripts s
                    LEFT JOIN app.Departments d ON s.DepartmentId = d.DepartmentId
                    WHERE s.IsActive = 1
                    ORDER BY d.DepartmentName, s.Name";

                return await connection.QueryAsync<Script>(sql);
            });
        }

        public async Task<Script?> GetScriptByIdAsync(int scriptId)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT s.ScriptId, s.Name, s.Description, s.SqlText, s.Category,
                           s.NeedsDateFrom, s.NeedsDateTo, s.IsActive,
                           s.DepartmentId, d.DepartmentName
                    FROM app.Scripts s
                    LEFT JOIN app.Departments d ON s.DepartmentId = d.DepartmentId
                    WHERE s.ScriptId = @ScriptId";

                return await connection.QueryFirstOrDefaultAsync<Script>(sql, new { ScriptId = scriptId });
            });
        }

        public async Task UpdateScriptDepartmentAsync(int scriptId, int? departmentId)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = "UPDATE app.Scripts SET DepartmentId = @DepartmentId WHERE ScriptId = @ScriptId";
                await connection.ExecuteAsync(sql, new { ScriptId = scriptId, DepartmentId = departmentId });
            });
        }

        // ============================================
        // SETTINGS
        // ============================================

        public async Task<string?> GetSettingAsync(string key)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = "SELECT [Value] FROM app.Settings WHERE [Key] = @Key";
                return await connection.QueryFirstOrDefaultAsync<string>(sql, new { Key = key });
            });
        }

        public async Task SetSettingAsync(string key, string value)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    MERGE app.Settings AS target
                    USING (SELECT @Key AS [Key], @Value AS [Value]) AS src
                    ON target.[Key] = src.[Key]
                    WHEN MATCHED THEN UPDATE SET [Value] = src.[Value]
                    WHEN NOT MATCHED THEN INSERT ([Key], [Value]) VALUES (src.[Key], src.[Value]);";
                await connection.ExecuteAsync(sql, new { Key = key, Value = value });
            });
        }

        // ============================================
        // RUN LOGGING
        // ============================================

        public async Task<long> StartScriptRunAsync(
            string userId,
            int scriptId,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    INSERT INTO app.ScriptRuns
                        (UserId, ScriptId, StartedAt, Status, DateFrom, DateTo)
                    OUTPUT INSERTED.RunId
                    VALUES (@UserId, @ScriptId, @StartedAt, 'Running', @DateFrom, @DateTo);";

                return await connection.ExecuteScalarAsync<long>(sql, new
                {
                    UserId = userId,
                    ScriptId = scriptId,
                    StartedAt = DateTime.Now,
                    DateFrom = dateFrom,
                    DateTo = dateTo
                });
            });
        }

        public async Task FinishScriptRunSuccessAsync(
            long runId,
            int rowsAffected,
            string outputPath)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    UPDATE app.ScriptRuns
                    SET FinishedAt = @FinishedAt,
                        Status = 'Success',
                        RowsAffected = @RowsAffected,
                        OutputPath = @OutputPath
                    WHERE RunId = @RunId;";

                await connection.ExecuteAsync(sql, new
                {
                    RunId = runId,
                    FinishedAt = DateTime.Now,
                    RowsAffected = rowsAffected,
                    OutputPath = outputPath
                });
            });
        }

        public async Task FinishScriptRunFailedAsync(long runId, string errorMessage)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    UPDATE app.ScriptRuns
                    SET FinishedAt = @FinishedAt,
                        Status = 'Failed',
                        ErrorMessage = @ErrorMessage
                    WHERE RunId = @RunId;";

                await connection.ExecuteAsync(sql, new
                {
                    RunId = runId,
                    FinishedAt = DateTime.Now,
                    ErrorMessage = errorMessage
                });
            });
        }

        // ============================================
        // USER MANAGEMENT
        // ============================================

        public async Task CreateUserAsync(
            string userId,
            string displayName,
            string passwordHash,
            bool isAdmin,
            int? departmentId)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    INSERT INTO app.Users (UserId, DisplayName, PasswordHash, IsAdmin, IsActive, DepartmentId)
                    VALUES (@UserId, @DisplayName, @PasswordHash, @IsAdmin, 1, @DepartmentId);";

                await connection.ExecuteAsync(sql, new
                {
                    UserId = userId,
                    DisplayName = displayName,
                    PasswordHash = passwordHash,
                    IsAdmin = isAdmin,
                    DepartmentId = departmentId
                });
            });
        }

        public async Task UpdateUserAsync(
            string userId,
            string displayName,
            bool isAdmin,
            int? departmentId)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    UPDATE app.Users
                    SET DisplayName = @DisplayName,
                        IsAdmin = @IsAdmin,
                        DepartmentId = @DepartmentId
                    WHERE UserId = @UserId;";

                await connection.ExecuteAsync(sql, new
                {
                    UserId = userId,
                    DisplayName = displayName,
                    IsAdmin = isAdmin,
                    DepartmentId = departmentId
                });
            });
        }

        public async Task SetUserActiveAsync(string userId, bool isActive)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = "UPDATE app.Users SET IsActive = @IsActive WHERE UserId = @UserId;";
                await connection.ExecuteAsync(sql, new { UserId = userId, IsActive = isActive });
            });
        }

        // ============================================
        // ACCESS LOG QUERIES
        // ============================================

        public async Task<DataTable> GetScriptRunsAsync(
            DateTime? dateFrom,
            DateTime? dateTo,
            string? userId,
            string? status)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();

                var sql = @"
                    SELECT 
                        sr.RunId,
                        sr.StartedAt,
                        sr.FinishedAt,
                        u.UserId,
                        u.DisplayName,
                        s.Name AS ScriptName,
                        sr.Status,
                        sr.RowsAffected,
                        sr.DateFrom,
                        sr.DateTo,
                        sr.OutputPath,
                        sr.ErrorMessage
                    FROM app.ScriptRuns sr
                    INNER JOIN app.Users u ON sr.UserId = u.UserId
                    INNER JOIN app.Scripts s ON sr.ScriptId = s.ScriptId
                    WHERE 1=1";

                var p = new DynamicParameters();

                if (dateFrom.HasValue)
                {
                    sql += " AND sr.StartedAt >= @DateFrom";
                    p.Add("@DateFrom", dateFrom.Value.Date);
                }
                if (dateTo.HasValue)
                {
                    sql += " AND sr.StartedAt < @DateTo";
                    p.Add("@DateTo", dateTo.Value.Date.AddDays(1));
                }
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    sql += " AND u.UserId = @UserId";
                    p.Add("@UserId", userId);
                }
                if (!string.IsNullOrWhiteSpace(status))
                {
                    sql += " AND sr.Status = @Status";
                    p.Add("@Status", status);
                }

                sql += " ORDER BY sr.RunId DESC;";

                var table = new DataTable();
                using var reader = await connection.ExecuteReaderAsync(sql, p);
                table.Load(reader);
                return table;
            });
        }

        public async Task LogAdminActionAsync(
            string userId,
            string actionType,
            string? targetType,
            string? targetId,
            string? details)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    INSERT INTO app.AdminActions (UserId, ActionType, TargetType, TargetId, Details)
                    VALUES (@UserId, @ActionType, @TargetType, @TargetId, @Details);";

                await connection.ExecuteAsync(sql, new
                {
                    UserId = userId,
                    ActionType = actionType,
                    TargetType = targetType,
                    TargetId = targetId,
                    Details = details
                });
            });
        }

        public async Task<DataTable> GetAdminActionsAsync(DateTime? dateFrom, DateTime? dateTo)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();

                var sql = @"
                    SELECT ActionId, Timestamp, UserId, ActionType, TargetType, TargetId, Details
                    FROM app.AdminActions
                    WHERE 1=1";

                var p = new DynamicParameters();
                if (dateFrom.HasValue) { sql += " AND Timestamp >= @DateFrom"; p.Add("@DateFrom", dateFrom.Value.Date); }
                if (dateTo.HasValue) { sql += " AND Timestamp < @DateTo"; p.Add("@DateTo", dateTo.Value.Date.AddDays(1)); }
                sql += " ORDER BY ActionId DESC;";

                var table = new DataTable();
                using var reader = await connection.ExecuteReaderAsync(sql, p);
                table.Load(reader);
                return table;
            });
        }

        // ============================================
        // RESILIENCE HELPERS
        // ============================================

        /// <summary>
        /// Runs a SQL operation with an exponential-backoff retry policy
        /// for transient errors. Retries up to 3 times (1s, 2s, 4s delays).
        /// </summary>
        private async Task<T> RunWithRetryAsync<T>(Func<Task<T>> operation)
        {
            var policy = Policy
                .Handle<SqlException>(ex => IsTransient(ex))
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));

            return await policy.ExecuteAsync(operation);
        }

        /// <summary>
        /// Void variant of <see cref="RunWithRetryAsync{T}"/>.
        /// </summary>
        private async Task RunWithRetryAsync(Func<Task> operation)
        {
            var policy = Policy
                .Handle<SqlException>(ex => IsTransient(ex))
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));

            await policy.ExecuteAsync(operation);
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