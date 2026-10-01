USE KabarakScriptRunner;
GO

-- ============================================
-- SCRIPT 19 — Students Admitted Per Year
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Students Admitted Per Year')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Students Admitted Per Year',
    'Count of students admitted each year, within a date range',
    'SELECT YEAR(EnrolmentDate) AS AdmissionYear, COUNT(*) AS StudentsAdmitted
     FROM uni.Students
     WHERE EnrolmentDate BETWEEN @DateFrom AND @DateTo
     GROUP BY YEAR(EnrolmentDate)
     ORDER BY AdmissionYear DESC;',
    'Registry', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
);
GO

-- ============================================
-- SCRIPT 20 — Active vs Inactive Students
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Active vs Inactive Students')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Active vs Inactive Students',
    'Breakdown of active vs inactive student records in a date range',
    'SELECT 
         CASE WHEN IsActive = 1 THEN ''Active'' ELSE ''Inactive'' END AS Status,
         COUNT(*) AS StudentCount
     FROM uni.Students
     WHERE EnrolmentDate BETWEEN @DateFrom AND @DateTo
     GROUP BY IsActive
     ORDER BY IsActive DESC;',
    'Registry', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
);
GO

-- ============================================
-- SCRIPT 21 — Top Courses by Enrolment
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Top Courses by Enrolment')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Top Courses by Enrolment',
    'Top 25 most-enrolled courses within a date range',
    'SELECT TOP 25 
         c.CourseCode, c.CourseName, 
         COUNT(e.EnrolmentId) AS EnrolmentCount
     FROM uni.Enrolments e
     INNER JOIN uni.Courses c ON e.CourseId = c.CourseId
     WHERE e.EnrolDate BETWEEN @DateFrom AND @DateTo
     GROUP BY c.CourseCode, c.CourseName
     ORDER BY EnrolmentCount DESC;',
    'Registry', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
);
GO

-- ============================================
-- SCRIPT 22 — Monthly Payment Trend
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Monthly Payment Trend')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Monthly Payment Trend',
    'Monthly totals of fee payments across the selected date range',
    'SELECT 
         FORMAT(PaymentDate, ''yyyy-MM'') AS Month,
         COUNT(*) AS Transactions,
         SUM(Amount) AS TotalCollected
     FROM uni.Payments
     WHERE PaymentDate BETWEEN @DateFrom AND @DateTo
     GROUP BY FORMAT(PaymentDate, ''yyyy-MM'')
     ORDER BY Month DESC;',
    'Finance', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Finance')
);
GO

-- ============================================
-- SCRIPT 23 — Highest Outstanding Balances
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Highest Outstanding Balances')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Highest Outstanding Balances',
    'Top 50 students with the largest outstanding fee balances in the date range',
    'SELECT TOP 50
         s.AdmissionNumber, s.FirstName, s.LastName, p.ProgrammeName,
         ISNULL(inv.TotalInvoiced, 0) AS TotalInvoiced,
         ISNULL(pay.TotalPaid, 0) AS TotalPaid,
         ISNULL(inv.TotalInvoiced, 0) - ISNULL(pay.TotalPaid, 0) AS Outstanding
     FROM uni.Students s
     LEFT JOIN uni.Programmes p ON s.ProgrammeId = p.ProgrammeId
     LEFT JOIN (
         SELECT StudentId, SUM(Amount) AS TotalInvoiced
         FROM uni.Invoices
         WHERE InvoiceDate BETWEEN @DateFrom AND @DateTo
         GROUP BY StudentId
     ) inv ON inv.StudentId = s.StudentId
     LEFT JOIN (
         SELECT StudentId, SUM(Amount) AS TotalPaid
         FROM uni.Payments
         WHERE PaymentDate BETWEEN @DateFrom AND @DateTo
         GROUP BY StudentId
     ) pay ON pay.StudentId = s.StudentId
     WHERE s.IsActive = 1
     ORDER BY Outstanding DESC;',
    'Finance', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Finance')
);
GO

-- ============================================
-- SCRIPT 24 — Exam Performance by Programme
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Exam Performance by Programme')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Exam Performance by Programme',
    'Average exam score and pass rate per programme within a date range',
    'SELECT 
         p.ProgrammeName,
         COUNT(r.ResultId) AS ResultCount,
         AVG(r.Score) AS AvgScore,
         CAST(100.0 * SUM(CASE WHEN r.Score >= 40 THEN 1 ELSE 0 END) / NULLIF(COUNT(r.ResultId), 0) AS DECIMAL(5,2)) AS PassRatePercent
     FROM uni.Results r
     INNER JOIN uni.Students s ON r.StudentId = s.StudentId
     INNER JOIN uni.Programmes p ON s.ProgrammeId = p.ProgrammeId
     INNER JOIN uni.Semesters sem ON r.SemesterId = sem.SemesterId
     WHERE sem.StartDate >= @DateFrom AND sem.EndDate <= @DateTo
     GROUP BY p.ProgrammeName
     ORDER BY AvgScore DESC;',
    'Examinations', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Examinations')
);
GO

PRINT 'Batch 4 scripts inserted.';
GO