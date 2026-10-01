USE KabarakScriptRunner;
GO

-- ============================================
-- SCRIPT 25 — Staff by Position
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Staff by Position')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Staff by Position',
    'Active staff grouped by their position, filtered by hire date',
    'SELECT Position, COUNT(*) AS Headcount, AVG(Salary) AS AvgSalary
     FROM uni.Employees
     WHERE IsActive = 1 AND HireDate BETWEEN @DateFrom AND @DateTo
     GROUP BY Position
     ORDER BY Headcount DESC;',
    'Human Resources', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Human Resources')
);
GO

-- ============================================
-- SCRIPT 26 — New Hires in Date Range
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'New Hires in Date Range')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'New Hires in Date Range',
    'Employees hired within the selected date range',
    'SELECT StaffNumber, FirstName, LastName, Department, Position, HireDate, Salary
     FROM uni.Employees
     WHERE HireDate BETWEEN @DateFrom AND @DateTo
     ORDER BY HireDate DESC;',
    'Human Resources', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Human Resources')
);
GO

-- ============================================
-- SCRIPT 27 — Payroll Summary by Department
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Payroll Summary by Department')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Payroll Summary by Department',
    'Total and average salaries per department',
    'SELECT Department, COUNT(*) AS StaffCount, SUM(Salary) AS TotalPayroll, AVG(Salary) AS AvgSalary
     FROM uni.Employees
     WHERE IsActive = 1 AND HireDate BETWEEN @DateFrom AND @DateTo
     GROUP BY Department
     ORDER BY TotalPayroll DESC;',
    'Human Resources', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Human Resources')
);
GO

-- ============================================
-- SCRIPT 28 — Course Pass Rate Trend
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Course Pass Rate Trend')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Course Pass Rate Trend',
    'Pass rate and average score per course, ordered by pass rate',
    'SELECT 
         c.CourseCode, c.CourseName,
         COUNT(r.ResultId) AS TotalResults,
         AVG(r.Score) AS AvgScore,
         CAST(100.0 * SUM(CASE WHEN r.Score >= 40 THEN 1 ELSE 0 END) / NULLIF(COUNT(r.ResultId), 0) AS DECIMAL(5,2)) AS PassRatePercent
     FROM uni.Results r
     INNER JOIN uni.Courses c ON r.CourseId = c.CourseId
     INNER JOIN uni.Semesters sem ON r.SemesterId = sem.SemesterId
     WHERE sem.StartDate >= @DateFrom AND sem.EndDate <= @DateTo
     GROUP BY c.CourseCode, c.CourseName
     ORDER BY PassRatePercent ASC, TotalResults DESC;',
    'Examinations', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Examinations')
);
GO

-- ============================================
-- SCRIPT 29 — Department Contact List
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Department Contact List')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Department Contact List',
    'All active employees grouped by department with positions',
    'SELECT Department, StaffNumber, FirstName, LastName, Position, HireDate
     FROM uni.Employees
     WHERE IsActive = 1 AND HireDate BETWEEN @DateFrom AND @DateTo
     ORDER BY Department, LastName;',
    'Administration', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Administration')
);
GO

-- ============================================
-- SCRIPT 30 — Yearly Summary Report
-- ============================================
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Yearly Summary Report')
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Yearly Summary Report',
    'High-level yearly summary: enrolments, payments, results',
    'SELECT 
         YEAR(s.EnrolmentDate) AS Year,
         COUNT(DISTINCT s.StudentId) AS StudentsEnrolled,
         (SELECT ISNULL(SUM(Amount), 0) 
          FROM uni.Payments 
          WHERE YEAR(PaymentDate) = YEAR(s.EnrolmentDate)
            AND PaymentDate BETWEEN @DateFrom AND @DateTo) AS FeesCollected,
         (SELECT COUNT(*) 
          FROM uni.Results r 
          INNER JOIN uni.Semesters sem ON r.SemesterId = sem.SemesterId
          WHERE YEAR(sem.StartDate) = YEAR(s.EnrolmentDate)
            AND sem.StartDate >= @DateFrom) AS ExamResults
     FROM uni.Students s
     WHERE s.EnrolmentDate BETWEEN @DateFrom AND @DateTo
     GROUP BY YEAR(s.EnrolmentDate)
     ORDER BY Year DESC;',
    'Administration', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Administration')
);
GO

PRINT 'Batch 5 scripts inserted.';
GO