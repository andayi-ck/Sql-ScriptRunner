using Dapper;
using KabarakScriptRunner.Data.Models;

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
            using var connection = _context.CreateConnection();
            const string sql = @"
                SELECT u.UserId, u.DisplayName, u.PasswordHash, u.IsAdmin, u.IsActive,
                       u.DepartmentId, d.DepartmentName
                FROM app.Users u
                LEFT JOIN app.Departments d ON u.DepartmentId = d.DepartmentId
                WHERE u.UserId = @UserId AND u.IsActive = 1";

            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId });
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            using var connection = _context.CreateConnection();
            const string sql = @"
                SELECT u.UserId, u.DisplayName, u.PasswordHash, u.IsAdmin, u.IsActive,
                       u.DepartmentId, d.DepartmentName
                FROM app.Users u
                LEFT JOIN app.Departments d ON u.DepartmentId = d.DepartmentId
                ORDER BY u.DisplayName";

            return await connection.QueryAsync<User>(sql);
        }

        public async Task UpdateUserPasswordAsync(string userId, string newPasswordHash)
        {
            using var connection = _context.CreateConnection();
            const string sql = "UPDATE app.Users SET PasswordHash = @Hash WHERE UserId = @UserId";
            await connection.ExecuteAsync(sql, new { Hash = newPasswordHash, UserId = userId });
        }

        // ============================================
        // DEPARTMENTS
        // ============================================

        public async Task<IEnumerable<Department>> GetDepartmentsAsync()
        {
            using var connection = _context.CreateConnection();
            const string sql = "SELECT DepartmentId, DepartmentName, IsActive FROM app.Departments WHERE IsActive = 1 ORDER BY DepartmentName";
            return await connection.QueryAsync<Department>(sql);
        }

        // ============================================
        // SCRIPTS
        // ============================================

        /// <summary>
        /// Returns all active scripts. If departmentId is provided, filters by department.
        /// Admin sees everything; regular users see their own department + scripts with no department.
        /// </summary>
        public async Task<IEnumerable<Script>> GetScriptsAsync(int? departmentId = null, bool isAdmin = false)
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
        }

        public async Task<Script?> GetScriptByIdAsync(int scriptId)
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
        }

        // ============================================
        // SETTINGS
        // ============================================

        public async Task<string?> GetSettingAsync(string key)
        {
            using var connection = _context.CreateConnection();
            const string sql = "SELECT [Value] FROM app.Settings WHERE [Key] = @Key";
            return await connection.QueryFirstOrDefaultAsync<string>(sql, new { Key = key });
        }

        public async Task SetSettingAsync(string key, string value)
        {
            using var connection = _context.CreateConnection();
            const string sql = @"
                MERGE app.Settings AS target
                USING (SELECT @Key AS [Key], @Value AS [Value]) AS src
                ON target.[Key] = src.[Key]
                WHEN MATCHED THEN UPDATE SET [Value] = src.[Value]
                WHEN NOT MATCHED THEN INSERT ([Key], [Value]) VALUES (src.[Key], src.[Value]);";
            await connection.ExecuteAsync(sql, new { Key = key, Value = value });
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
            using var connection = _context.CreateConnection();
            const string sql = @"
                INSERT INTO app.ScriptRuns
                    (UserId, ScriptId, StartedAt, Status, DateFrom, DateTo)
                OUTPUT INSERTED.RunId
                VALUES (@UserId, @ScriptId, @StartedAt, 'Running', @DateFrom, @DateTo);";

            var runId = await connection.ExecuteScalarAsync<long>(sql, new
            {
                UserId = userId,
                ScriptId = scriptId,
                StartedAt = DateTime.Now,
                DateFrom = dateFrom,
                DateTo = dateTo
            });

            return runId;
        }

        public async Task FinishScriptRunSuccessAsync(
            long runId,
            int rowsAffected,
            string outputPath)
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
        }

        public async Task FinishScriptRunFailedAsync(long runId, string errorMessage)
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
        }


        // ============================================
        // USER MANAGEMENT (Phase 3)
        // ============================================

        public async Task CreateUserAsync(
            string userId,
            string displayName,
            string passwordHash,
            bool isAdmin,
            int? departmentId)
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
        }

        public async Task UpdateUserAsync(
            string userId,
            string displayName,
            bool isAdmin,
            int? departmentId)
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
        }

        public async Task SetUserActiveAsync(string userId, bool isActive)
        {
            using var connection = _context.CreateConnection();
            const string sql = "UPDATE app.Users SET IsActive = @IsActive WHERE UserId = @UserId;";
            await connection.ExecuteAsync(sql, new { UserId = userId, IsActive = isActive });
        }


        public async Task UpdateScriptDepartmentAsync(int scriptId, int? departmentId)
        {
            using var connection = _context.CreateConnection();
            const string sql = "UPDATE app.Scripts SET DepartmentId = @DepartmentId WHERE ScriptId = @ScriptId";
            await connection.ExecuteAsync(sql, new { ScriptId = scriptId, DepartmentId = departmentId });
        }


        public async Task<System.Data.DataTable> GetScriptRunsAsync(
             DateTime? dateFrom,
             DateTime? dateTo,
             string? userId,
             string? status)
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

            var table = new System.Data.DataTable();
            using var reader = await connection.ExecuteReaderAsync(sql, p);
            table.Load(reader);
            return table;
        }

        public async Task LogAdminActionAsync(
            string userId,
            string actionType,
            string? targetType,
            string? targetId,
            string? details)
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
        }

        public async Task<System.Data.DataTable> GetAdminActionsAsync(DateTime? dateFrom, DateTime? dateTo)
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

            var table = new System.Data.DataTable();
            using var reader = await connection.ExecuteReaderAsync(sql, p);
            table.Load(reader);
            return table;
        }



    }
}