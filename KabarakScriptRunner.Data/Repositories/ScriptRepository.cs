using System;
using System.Collections.Generic;
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
                           u.DepartmentId, d.DepartmentName,
                           u.FailedAttempts, u.LockoutEnd  -- <--- ADD THESE TWO COLUMNS
                    FROM app.Users u
                    LEFT JOIN app.Departments d ON u.DepartmentId = d.DepartmentId
                    WHERE u.UserId = @UserId AND u.IsActive = 1";

                return await connection.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId });
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

        public async Task<IEnumerable<UserSummary>> GetAllUsersAsync()
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT u.UserId, u.DisplayName, u.IsAdmin, u.IsActive,
                           u.DepartmentId, d.DepartmentName
                    FROM app.Users u
                    LEFT JOIN app.Departments d ON u.DepartmentId = d.DepartmentId
                    ORDER BY u.DisplayName";

                return await connection.QueryAsync<UserSummary>(sql);
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
                    SELECT s.ScriptId, s.Version, s.Name, s.Description, s.SqlText, s.Category,
                           s.NeedsDateFrom, s.NeedsDateTo, s.IsActive, s.TargetDatabase,
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
                    SELECT s.ScriptId, s.Version, s.Name, s.Description, s.SqlText, s.Category,
                           s.NeedsDateFrom, s.NeedsDateTo, s.IsActive, s.TargetDatabase,
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
        // SCRIPT VERSIONING
        // ============================================

        public async Task<IEnumerable<ScriptVersion>> GetScriptVersionsAsync(int scriptId)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT VersionId, ScriptId, Version, Name, Description, SqlText,
                           Category, DepartmentId, NeedsDateFrom, NeedsDateTo, IsActive,
                           ArchivedAt, ArchivedBy
                    FROM app.ScriptVersions
                    WHERE ScriptId = @ScriptId
                    ORDER BY Version DESC;";

                return await connection.QueryAsync<ScriptVersion>(sql, new { ScriptId = scriptId });
            });
        }

        public async Task<ScriptVersion?> GetScriptVersionAsync(int scriptId, int version)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT VersionId, ScriptId, Version, Name, Description, SqlText,
                           Category, DepartmentId, NeedsDateFrom, NeedsDateTo, IsActive,
                           ArchivedAt, ArchivedBy
                    FROM app.ScriptVersions
                    WHERE ScriptId = @ScriptId AND Version = @Version;";

                return await connection.QueryFirstOrDefaultAsync<ScriptVersion>(
                    sql, new { ScriptId = scriptId, Version = version });
            });
        }


        public async Task IncrementFailedAttemptsAsync(string userId, int currentAttempts)
        {
            using var connection = _context.CreateConnection();
            if (currentAttempts + 1 >= 5)
            {
                // Lock out for 5 minutes
                await connection.ExecuteAsync(@"
                    UPDATE app.Users 
                    SET FailedAttempts = FailedAttempts + 1, 
                        LockoutEnd = DATEADD(MINUTE, 5, GETDATE()) 
                    WHERE UserId = @UserId", new { UserId = userId });
            }
            else
            {
                await connection.ExecuteAsync(
                    "UPDATE app.Users SET FailedAttempts = FailedAttempts + 1 WHERE UserId = @UserId",
                    new { UserId = userId });
            }
        }

        public async Task ResetFailedAttemptsAsync(string userId)
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(
                "UPDATE app.Users SET FailedAttempts = 0, LockoutEnd = NULL WHERE UserId = @UserId",
                new { UserId = userId });
        }


        public async Task RestoreScriptVersionAsync(int scriptId, int versionToRestore)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    UPDATE app.Scripts
                    SET Name          = v.Name,
                        Description   = v.Description,
                        SqlText       = v.SqlText,
                        Category      = v.Category,
                        DepartmentId  = v.DepartmentId,
                        NeedsDateFrom = v.NeedsDateFrom,
                        NeedsDateTo   = v.NeedsDateTo,
                        IsActive      = v.IsActive
                    FROM app.Scripts s
                    INNER JOIN app.ScriptVersions v
                        ON v.ScriptId = s.ScriptId AND v.Version = @Version
                    WHERE s.ScriptId = @ScriptId;";

                await connection.ExecuteAsync(sql, new { ScriptId = scriptId, Version = versionToRestore });
            });
        }

        // ============================================
        // SCRIPT COMPILER / EDITOR
        // ============================================

        

        public async Task<(bool IsDuplicate, string Reason)> CheckScriptDuplicateAsync(string name, string sqlText)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();

                var existingByName = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT Name FROM app.Scripts WHERE Name = @Name", new { Name = name });

                if (existingByName != null)
                    return (true, $"A script with the name '{name}' already exists.");

                var existingBySql = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT Name FROM app.Scripts WHERE SqlText = @SqlText", new { SqlText = sqlText });

                if (existingBySql != null)
                    return (true, $"This exact SQL query already exists in the script named '{existingBySql}'.");

                return (false, string.Empty);
            });
        }

        // Renamed to CreateUserAsync to avoid conflict with CreateScriptAsync
        public async Task CreateUserAsync(string userId, string displayName, string passwordHash, bool isAdmin, int? departmentId)
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

        public async Task CreateScriptAsync(
            string name,
            string description,
            string sqlText,
            string category,
            string targetDatabase,
            bool needsDateFrom,
            bool needsDateTo,
            int? departmentId)
        {
            await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    INSERT INTO app.Scripts 
                        (Name, Description, SqlText, Category, TargetDatabase, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId, Version)
                    VALUES 
                        (@Name, @Description, @SqlText, @Category, @TargetDatabase, @NeedsDateFrom, @NeedsDateTo, 1, @DepartmentId, 1);";

                await connection.ExecuteAsync(sql, new
                {
                    Name = name,
                    Description = description,
                    SqlText = sqlText,
                    Category = category,
                    TargetDatabase = targetDatabase,
                    NeedsDateFrom = needsDateFrom,
                    NeedsDateTo = needsDateTo,
                    DepartmentId = departmentId
                });
            });
        }

        public async Task UpdateUserAsync(string userId, string displayName, bool isAdmin, int? departmentId)
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

        public async Task<long> StartScriptRunAsync(string userId, int scriptId, DateTime? dateFrom, DateTime? dateTo)
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

        public async Task FinishScriptRunSuccessAsync(long runId, int rowsAffected, string outputPath)
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
        // ACCESS LOG QUERIES
        // ============================================

        public async Task<DataTable> GetScriptRunsAsync(DateTime? dateFrom, DateTime? dateTo, string? userId, string? status)
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

        public async Task LogAdminActionAsync(string userId, string actionType, string? targetType, string? targetId, string? details)
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

        private static bool IsTransient(SqlException ex)
        {
            int[] transientErrors = { -2, 20, 64, 233, 10053, 10054, 10060, 1205, 4060, 10928, 10929, 40197, 40501, 40613, 49918, 49919, 49920 };
            return Array.IndexOf(transientErrors, ex.Number) >= 0;
        }
    }
}