
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Students with Outstanding Fees',
    'Students whose invoiced fees exceed payments received in the date range',
    'SELECT s.AdmissionNumber, s.FirstName, s.LastName, p.ProgrammeName,
            ISNULL(SUM(i.Amount), 0) AS Invoiced,
            ISNULL((SELECT SUM(pm.Amount) FROM uni.Payments pm WHERE pm.StudentId = s.StudentId AND pm.PaymentDate BETWEEN @DateFrom AND @DateTo), 0) AS Paid,
            ISNULL(SUM(i.Amount), 0) - ISNULL((SELECT SUM(pm.Amount) FROM uni.Payments pm WHERE pm.StudentId = s.StudentId AND pm.PaymentDate BETWEEN @DateFrom AND @DateTo), 0) AS Outstanding
     FROM uni.Students s
     LEFT JOIN uni.Programmes p ON s.ProgrammeId = p.ProgrammeId
     LEFT JOIN uni.Invoices i ON i.StudentId = s.StudentId AND i.InvoiceDate BETWEEN @DateFrom AND @DateTo
     WHERE s.IsActive = 1
     GROUP BY s.AdmissionNumber, s.FirstName, s.LastName, p.ProgrammeName, s.StudentId
     HAVING ISNULL(SUM(i.Amount), 0) - ISNULL((SELECT SUM(pm.Amount) FROM uni.Payments pm WHERE pm.StudentId = s.StudentId AND pm.PaymentDate BETWEEN @DateFrom AND @DateTo), 0) > 0
     ORDER BY Outstanding DESC;',
    'Finance', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Finance')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Daily Payment Totals',
    'Total fee payments per day within a date range',
    'SELECT CAST(PaymentDate AS DATE) AS PaymentDate,
            COUNT(*) AS Transactions,
            SUM(Amount) AS DailyTotal
     FROM uni.Payments
     WHERE PaymentDate BETWEEN @DateFrom AND @DateTo
     GROUP BY CAST(PaymentDate AS DATE)
     ORDER BY PaymentDate DESC;',
    'Finance', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Finance')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Programme Fee Summary',
    'Total fees collected per programme in a date range',
    'SELECT p.ProgrammeName,
            COUNT(DISTINCT s.StudentId) AS Students,
            ISNULL(SUM(pm.Amount), 0) AS FeesCollected
     FROM uni.Students s
     INNER JOIN uni.Programmes p ON s.ProgrammeId = p.ProgrammeId
     LEFT JOIN uni.Payments pm ON pm.StudentId = s.StudentId AND pm.PaymentDate BETWEEN @DateFrom AND @DateTo
     GROUP BY p.ProgrammeName
     ORDER BY FeesCollected DESC;',
    'Finance', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Finance')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Programme Fee Summary',
    'Total fees collected per programme in a date range',
    'SELECT p.ProgrammeName,
            COUNT(DISTINCT s.StudentId) AS Students,
            ISNULL(SUM(pm.Amount), 0) AS FeesCollected
     FROM uni.Students s
     INNER JOIN uni.Programmes p ON s.ProgrammeId = p.ProgrammeId
     LEFT JOIN uni.Payments pm ON pm.StudentId = s.StudentId AND pm.PaymentDate BETWEEN @DateFrom AND @DateTo
     GROUP BY p.ProgrammeName
     ORDER BY FeesCollected DESC;',
    'Finance', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Finance')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Staff Directory',
    'List of employees in the HR directory, filtered by hire date range',
    'SELECT StaffNumber, FirstName, LastName, Department, Position, HireDate, Salary
     FROM uni.Employees
     WHERE IsActive = 1 AND HireDate BETWEEN @DateFrom AND @DateTo
     ORDER BY Department, LastName;',
    'Human Resources', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Human Resources')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Staff Headcount by Department',
    'Number of active employees per department, filtered by hire date',
    'SELECT Department, COUNT(*) AS Headcount, AVG(Salary) AS AverageSalary
     FROM uni.Employees
     WHERE IsActive = 1 AND HireDate BETWEEN @DateFrom AND @DateTo
     GROUP BY Department
     ORDER BY Headcount DESC;',
    'Human Resources', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Human Resources')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Enrolment Activity Log',
    'Detailed list of enrolment activities in a date range',
    'SELECT e.EnrolDate, s.AdmissionNumber, s.FirstName, s.LastName, c.CourseCode, c.CourseName, e.Status
     FROM uni.Enrolments e
     INNER JOIN uni.Students s ON e.StudentId = s.StudentId
     INNER JOIN uni.Courses  c ON e.CourseId  = c.CourseId
     WHERE e.EnrolDate BETWEEN @DateFrom AND @DateTo
     ORDER BY e.EnrolDate DESC;',
    'Registry', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
);




