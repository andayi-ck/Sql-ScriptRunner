
INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Enrolment Summary by Programme',
    'Number of students enrolled per programme in a date range',
    'SELECT p.ProgrammeName, p.School, COUNT(DISTINCT s.StudentId) AS StudentCount
     FROM uni.Students s
     INNER JOIN uni.Programmes p ON s.ProgrammeId = p.ProgrammeId
     WHERE s.EnrolmentDate BETWEEN @DateFrom AND @DateTo
     GROUP BY p.ProgrammeName, p.School
     ORDER BY StudentCount DESC;',
    'Registry', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
);



INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Active Students by Gender',
    'Gender distribution of students enrolled in a date range',
    'SELECT Gender, COUNT(*) AS Total
     FROM uni.Students
     WHERE EnrolmentDate BETWEEN @DateFrom AND @DateTo AND IsActive = 1
     GROUP BY Gender
     ORDER BY Total DESC;',
    'Registry', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
);



INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'New Students Report',
    'List of newly admitted students in a date range',
    'SELECT s.AdmissionNumber, s.FirstName, s.LastName, s.Gender, p.ProgrammeName, s.EnrolmentDate
     FROM uni.Students s
     LEFT JOIN uni.Programmes p ON s.ProgrammeId = p.ProgrammeId
     WHERE s.EnrolmentDate BETWEEN @DateFrom AND @DateTo
     ORDER BY s.EnrolmentDate DESC, s.LastName;',
    'Registry', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Fees Collected by Date Range',
    'All fee payments received within a date range',
    'SELECT p.PaymentDate, s.AdmissionNumber, s.FirstName, s.LastName, p.Amount, p.Method, p.Reference
     FROM uni.Payments p
     INNER JOIN uni.Students s ON p.StudentId = s.StudentId
     WHERE p.PaymentDate BETWEEN @DateFrom AND @DateTo
     ORDER BY p.PaymentDate DESC;',
    'Finance', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Finance')
);




INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Payments Summary by Method',
    'Total fee payments grouped by payment method in a date range',
    'SELECT Method, COUNT(*) AS PaymentCount, SUM(Amount) AS TotalAmount
     FROM uni.Payments
     WHERE PaymentDate BETWEEN @DateFrom AND @DateTo
     GROUP BY Method
     ORDER BY TotalAmount DESC;',
    'Finance', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Finance')
);



INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Exam Results Summary',
    'Distribution of grades across all courses in a date range',
    'SELECT r.Grade, COUNT(*) AS StudentCount, AVG(r.Score) AS AvgScore
     FROM uni.Results r
     INNER JOIN uni.Semesters sem ON r.SemesterId = sem.SemesterId
     WHERE sem.StartDate >= @DateFrom AND sem.EndDate <= @DateTo
     GROUP BY r.Grade
     ORDER BY r.Grade;',
    'Examinations', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Examinations')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Student List by Programme',
    'All students in a selected date range, grouped by programme',
    'SELECT p.ProgrammeName, s.AdmissionNumber, s.FirstName, s.LastName, s.Gender, s.YearOfStudy, s.EnrolmentDate
     FROM uni.Students s
     INNER JOIN uni.Programmes p ON s.ProgrammeId = p.ProgrammeId
     WHERE s.EnrolmentDate BETWEEN @DateFrom AND @DateTo
     ORDER BY p.ProgrammeName, s.LastName, s.FirstName;',
    'Registry', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Enrolment by Course',
    'Number of students enrolled in each course within a date range',
    'SELECT c.CourseCode, c.CourseName, COUNT(e.EnrolmentId) AS EnrolledCount
     FROM uni.Enrolments e
     INNER JOIN uni.Courses c ON e.CourseId = c.CourseId
     WHERE e.EnrolDate BETWEEN @DateFrom AND @DateTo
     GROUP BY c.CourseCode, c.CourseName
     ORDER BY EnrolledCount DESC;',
    'Registry', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Students by Year of Study',
    'Distribution of students across years of study within a date range',
    'SELECT YearOfStudy, COUNT(*) AS StudentCount
     FROM uni.Students
     WHERE EnrolmentDate BETWEEN @DateFrom AND @DateTo AND IsActive = 1
     GROUP BY YearOfStudy
     ORDER BY YearOfStudy;',
    'Registry', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
);



INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Top Performing Students',
    'Students with the highest average score in a date range',
    'SELECT TOP 50 s.AdmissionNumber, s.FirstName, s.LastName, AVG(r.Score) AS AvgScore, COUNT(r.ResultId) AS CoursesCount
     FROM uni.Results r
     INNER JOIN uni.Students s ON r.StudentId = s.StudentId
     INNER JOIN uni.Semesters sem ON r.SemesterId = sem.SemesterId
     WHERE sem.StartDate >= @DateFrom AND sem.EndDate <= @DateTo
     GROUP BY s.AdmissionNumber, s.FirstName, s.LastName
     HAVING COUNT(r.ResultId) >= 3
     ORDER BY AvgScore DESC;',
    'Examinations', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Examinations')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Pass / Fail Summary',
    'Count of passing vs failing results in a date range',
    'SELECT 
         SUM(CASE WHEN r.Score >= 40 THEN 1 ELSE 0 END) AS PassCount,
         SUM(CASE WHEN r.Score < 40 THEN 1 ELSE 0 END) AS FailCount,
         COUNT(*) AS TotalResults,
         AVG(r.Score) AS AverageScore
     FROM uni.Results r
     INNER JOIN uni.Semesters sem ON r.SemesterId = sem.SemesterId
     WHERE sem.StartDate >= @DateFrom AND sem.EndDate <= @DateTo;',
    'Examinations', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Examinations')
);


INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo, IsActive, DepartmentId)
VALUES (
    'Course Performance Summary',
    'Average score and pass rate per course in a date range',
    'SELECT c.CourseCode, c.CourseName, 
            COUNT(r.ResultId) AS ResultCount,
            AVG(r.Score) AS AvgScore,
            CAST(100.0 * SUM(CASE WHEN r.Score >= 40 THEN 1 ELSE 0 END) / NULLIF(COUNT(r.ResultId), 0) AS DECIMAL(5,2)) AS PassRatePercent
     FROM uni.Results r
     INNER JOIN uni.Courses c ON r.CourseId = c.CourseId
     INNER JOIN uni.Semesters sem ON r.SemesterId = sem.SemesterId
     WHERE sem.StartDate >= @DateFrom AND sem.EndDate <= @DateTo
     GROUP BY c.CourseCode, c.CourseName
     ORDER BY AvgScore DESC;',
    'Examinations', 1, 1, 1,
    (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Examinations')
);

