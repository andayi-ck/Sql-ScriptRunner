using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Polly;

namespace KabarakScriptRunner.Data.Repositories
{
    /// <summary>
    /// Read-only queries that feed the dashboard charts.
    /// All operations are resilient (Polly retry on transient failures).
    /// </summary>
    public class DashboardRepository
    {
        private readonly DatabaseContext _context;

        public DashboardRepository(DatabaseContext context)
        {
            _context = context;
        }

        // ============================================
        // MONTHLY FEES COLLECTED
        // ============================================
        public async Task<IEnumerable<MonthlyTotal>> GetMonthlyFeesCollectedAsync(int monthsBack = 12)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    DECLARE @StartDate DATE = DATEADD(MONTH, -@MonthsBack, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1));

                    SELECT 
                        FORMAT(PaymentDate, 'yyyy-MM') AS Period,
                        SUM(Amount) AS Total
                    FROM uni.Payments
                    WHERE PaymentDate >= @StartDate
                    GROUP BY FORMAT(PaymentDate, 'yyyy-MM')
                    ORDER BY Period;";

                return await connection.QueryAsync<MonthlyTotal>(sql, new { MonthsBack = monthsBack });
            });
        }

        // ============================================
        // STUDENTS ADMITTED PER YEAR
        // ============================================
        public async Task<IEnumerable<YearlyTotal>> GetStudentsAdmittedPerYearAsync(int yearsBack = 5)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    DECLARE @StartYear INT = YEAR(GETDATE()) - @YearsBack;

                    SELECT 
                        YEAR(EnrolmentDate) AS [Year],
                        COUNT(*) AS Total
                    FROM uni.Students
                    WHERE YEAR(EnrolmentDate) >= @StartYear
                    GROUP BY YEAR(EnrolmentDate)
                    ORDER BY [Year];";

                return await connection.QueryAsync<YearlyTotal>(sql, new { YearsBack = yearsBack });
            });
        }

        // ============================================
        // TOP COURSES BY ENROLMENT
        // ============================================
        public async Task<IEnumerable<CourseCount>> GetTopCoursesByEnrolmentAsync(int top = 10)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT TOP (@Top)
                        c.CourseCode + ' — ' + c.CourseName AS Label,
                        COUNT(e.EnrolmentId) AS Total
                    FROM uni.Enrolments e
                    INNER JOIN uni.Courses c ON e.CourseId = c.CourseId
                    GROUP BY c.CourseCode, c.CourseName
                    ORDER BY COUNT(e.EnrolmentId) DESC;";

                return await connection.QueryAsync<CourseCount>(sql, new { Top = top });
            });
        }

        // ============================================
        // RECENT ACTIVITY
        // ============================================
        public async Task<IEnumerable<RecentActivity>> GetRecentActivityAsync(int top = 20)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT TOP (@Top)
                        a.Timestamp,
                        a.UserId,
                        a.ActionType,
                        a.Details
                    FROM app.AdminActions a
                    ORDER BY a.ActionId DESC;";

                return await connection.QueryAsync<RecentActivity>(sql, new { Top = top });
            });
        }

        // ============================================
        // SUMMARY COUNTS (KPIs)
        // ============================================
        public async Task<DashboardSummary> GetSummaryAsync()
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT
                        (SELECT COUNT(*) FROM uni.Students WHERE IsActive = 1) AS ActiveStudents,
                        (SELECT COUNT(*) FROM uni.Employees WHERE IsActive = 1) AS ActiveStaff,
                        (SELECT COUNT(*) FROM app.Scripts WHERE IsActive = 1) AS ActiveScripts,
                        (SELECT ISNULL(SUM(Amount), 0) FROM uni.Payments 
                            WHERE PaymentDate >= DATEADD(MONTH, -1, GETDATE())) AS FeesLast30Days,
                        (SELECT COUNT(*) FROM app.ScriptRuns 
                            WHERE StartedAt >= DATEADD(DAY, -7, GETDATE())) AS RunsLast7Days;";

                return await connection.QueryFirstAsync<DashboardSummary>(sql);
            });
        }

        // ============================================
        // AUDIT ANALYTICS  (Feature 6)
        // ============================================
        public async Task<IEnumerable<MonthlyTotal>> GetLoginsPerDayAsync(int daysBack = 30)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    DECLARE @StartDate DATETIME2 = DATEADD(DAY, -@DaysBack, GETDATE());

                    SELECT 
                        CONVERT(VARCHAR(10), CAST(Timestamp AS DATE), 23) AS Period,
                        COUNT(*) AS Total
                    FROM app.AdminActions
                    WHERE Timestamp >= @StartDate AND ActionType = 'LoginSuccess'
                    GROUP BY CAST(Timestamp AS DATE)
                    ORDER BY Period;";

                return await connection.QueryAsync<MonthlyTotal>(sql, new { DaysBack = daysBack });
            });
        }

        public async Task<IEnumerable<CourseCount>> GetTopScriptsByRunsAsync(int top = 10)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    SELECT TOP (@Top)
                        s.Name AS Label,
                        COUNT(*) AS Total
                    FROM app.ScriptRuns sr
                    INNER JOIN app.Scripts s ON sr.ScriptId = s.ScriptId
                    GROUP BY s.Name
                    ORDER BY COUNT(*) DESC;";

                return await connection.QueryAsync<CourseCount>(sql, new { Top = top });
            });
        }

        public async Task<LoginStats> GetLoginStatsAsync(int daysBack = 30)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    DECLARE @StartDate DATETIME2 = DATEADD(DAY, -@DaysBack, GETDATE());

                    SELECT
                        SUM(CASE WHEN ActionType = 'LoginSuccess' THEN 1 ELSE 0 END) AS Success,
                        SUM(CASE WHEN ActionType = 'LoginFailure' THEN 1 ELSE 0 END) AS Failed
                    FROM app.AdminActions
                    WHERE Timestamp >= @StartDate;";

                return await connection.QueryFirstAsync<LoginStats>(sql, new { DaysBack = daysBack });
            });
        }

        public async Task<IEnumerable<UserActionCount>> GetActionsByUserAsync(int daysBack = 30, int top = 15)
        {
            return await RunWithRetryAsync(async () =>
            {
                using var connection = _context.CreateConnection();
                const string sql = @"
                    DECLARE @StartDate DATETIME2 = DATEADD(DAY, -@DaysBack, GETDATE());

                    SELECT TOP (@Top)
                        a.UserId AS Label,
                        COUNT(*) AS Total
                    FROM app.AdminActions a
                    WHERE a.Timestamp >= @StartDate
                    GROUP BY a.UserId
                    ORDER BY COUNT(*) DESC;";

                return await connection.QueryAsync<UserActionCount>(sql, new { DaysBack = daysBack, Top = top });
            });
        }

        // ============================================
        // HELPERS
        // ============================================
        private async Task<T> RunWithRetryAsync<T>(Func<Task<T>> operation)
        {
            var policy = Policy
                .Handle<SqlException>(ex => IsTransient(ex))
                .Or<TimeoutException>()
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));

            return await policy.ExecuteAsync(operation);
        }

        private static bool IsTransient(SqlException ex)
        {
            int[] transientErrors = { -2, 20, 64, 233, 10053, 10054, 10060, 1205, 4060, 10928, 10929, 40197, 40501, 40613, 49918, 49919, 49920 };
            return Array.IndexOf(transientErrors, ex.Number) >= 0;
        }
    }

    // ============================================
    // DTOs
    // ============================================
    public class MonthlyTotal
    {
        public string Period { get; set; } = "";
        public decimal Total { get; set; }
    }

    public class YearlyTotal
    {
        public int Year { get; set; }
        public int Total { get; set; }
    }

    public class CourseCount
    {
        public string Label { get; set; } = "";
        public int Total { get; set; }
    }

    public class RecentActivity
    {
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; } = "";
        public string ActionType { get; set; } = "";
        public string? Details { get; set; }
    }

    public class DashboardSummary
    {
        public int ActiveStudents { get; set; }
        public int ActiveStaff { get; set; }
        public int ActiveScripts { get; set; }
        public decimal FeesLast30Days { get; set; }
        public int RunsLast7Days { get; set; }
    }

    public class LoginStats
    {
        public int Success { get; set; }
        public int Failed { get; set; }
    }

    public class UserActionCount
    {
        public string Label { get; set; } = "";
        public int Total { get; set; }
    }
}