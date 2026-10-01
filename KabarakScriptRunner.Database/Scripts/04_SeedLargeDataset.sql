USE KabarakScriptRunner;
GO

SET NOCOUNT ON;

-- ============================================
-- 1. Programmes (idempotent)
-- ============================================
IF NOT EXISTS (SELECT 1 FROM uni.Programmes)
BEGIN
    INSERT INTO uni.Programmes (ProgrammeCode, ProgrammeName, School, DurationYears) VALUES
        ('BSC-CS',  'BSc Computer Science',        'School of Science',       4),
        ('BSC-IT',  'BSc Information Technology',  'School of Science',       4),
        ('BSC-MTH', 'BSc Mathematics',             'School of Science',       4),
        ('BSC-BIO', 'BSc Biology',                 'School of Science',       4),
        ('BBA',     'Bachelor of Business Admin',  'School of Business',      4),
        ('BCOM',    'Bachelor of Commerce',        'School of Business',      4),
        ('BSED',    'Bachelor of Education',       'School of Education',     4),
        ('BTH',     'Bachelor of Theology',        'School of Theology',      3),
        ('BSC-NUR', 'BSc Nursing',                 'School of Health',        4),
        ('BSC-PH',  'BSc Public Health',           'School of Health',        4),
        ('LLB',     'Bachelor of Laws',            'School of Law',           4),
        ('BSC-AG',  'BSc Agriculture',             'School of Agriculture',   4),
        ('BA-ENG',  'BA English',                  'School of Arts',          3),
        ('BA-KIS',  'BA Kiswahili',                'School of Arts',          3),
        ('BA-JRN',  'BA Journalism',               'School of Arts',          3);
END
GO

-- ============================================
-- 2. Semesters (idempotent)
-- ============================================
IF NOT EXISTS (SELECT 1 FROM uni.Semesters)
BEGIN
    DECLARE @y INT = 2021;
    WHILE @y <= 2026
    BEGIN
        INSERT INTO uni.Semesters (SemesterName, StartDate, EndDate, IsActive) VALUES
            (CAST(@y AS NVARCHAR) + ' Semester 1', DATEFROMPARTS(@y, 1, 10), DATEFROMPARTS(@y, 5, 30), 0),
            (CAST(@y AS NVARCHAR) + ' Semester 2', DATEFROMPARTS(@y, 8, 20), DATEFROMPARTS(@y, 12, 15), CASE WHEN @y = 2026 THEN 1 ELSE 0 END);
        SET @y = @y + 1;
    END
END
GO

-- ============================================
-- 3. Courses (idempotent)
-- ============================================
IF NOT EXISTS (SELECT 1 FROM uni.Courses)
BEGIN
    DECLARE @pid INT, @i INT;
    DECLARE prog_cursor CURSOR FOR SELECT ProgrammeId FROM uni.Programmes;
    OPEN prog_cursor;
    FETCH NEXT FROM prog_cursor INTO @pid;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @i = 1;
        WHILE @i <= 15
        BEGIN
            INSERT INTO uni.Courses (CourseCode, CourseName, Credits, ProgrammeId)
            VALUES (
                'C' + CAST(@pid AS NVARCHAR) + '-' + RIGHT('00' + CAST(@i AS NVARCHAR), 2),
                'Course ' + CAST(@i AS NVARCHAR) + ' for Programme ' + CAST(@pid AS NVARCHAR),
                3,
                @pid
            );
            SET @i = @i + 1;
        END
        FETCH NEXT FROM prog_cursor INTO @pid;
    END
    CLOSE prog_cursor;
    DEALLOCATE prog_cursor;
END
GO

-- ============================================
-- 4. Students (5000) — using #temp tables
-- ============================================
IF (SELECT COUNT(*) FROM uni.Students) < 5000
BEGIN
    -- Temp tables survive GO boundaries
    IF OBJECT_ID('tempdb..#firstName') IS NOT NULL DROP TABLE #firstName;
    IF OBJECT_ID('tempdb..#lastName')  IS NOT NULL DROP TABLE #lastName;
    IF OBJECT_ID('tempdb..#progIds')   IS NOT NULL DROP TABLE #progIds;

    CREATE TABLE #firstName (Name NVARCHAR(50));
    CREATE TABLE #lastName  (Name NVARCHAR(50));

    INSERT INTO #firstName (Name) VALUES
        ('John'),('Mary'),('Peter'),('Grace'),('David'),('Faith'),('James'),('Mercy'),
        ('Samuel'),('Joyce'),('Daniel'),('Esther'),('Michael'),('Rose'),('Paul'),
        ('Catherine'),('Joseph'),('Susan'),('Brian'),('Lydia'),('Kevin'),('Sarah'),
        ('Evans'),('Naomi'),('Emmanuel'),('Nancy'),('Isaac'),('Purity'),('Eric'),('Doris');

    INSERT INTO #lastName (Name) VALUES
        ('Kimani'),('Wanjiku'),('Otieno'),('Cheruiyot'),('Kipchoge'),('Mwangi'),
        ('Odhiambo'),('Kariuki'),('Njuguna'),('Ochieng'),('Mutua'),('Wafula'),
        ('Kiptoo'),('Barasa'),('Njeri'),('Ngugi'),('Chebet'),('Nyambura'),
        ('Onyango'),('Korir'),('Waweru'),('Kamau'),('Auma'),('Maina'),
        ('Rotich'),('Mwikali'),('Owino'),('Kilonzo'),('Mbugua'),('Kiptanui');

    -- Get programme IDs once
    CREATE TABLE #progIds (Id INT);
    INSERT INTO #progIds (Id) SELECT ProgrammeId FROM uni.Programmes;

    DECLARE @n INT = 1;
    DECLARE @totalStudents INT = 5000;

    DECLARE @fn NVARCHAR(50), @ln NVARCHAR(50), @prog INT;
    DECLARE @gender NVARCHAR(10), @yob DATE, @enrol DATE, @yos INT;

    WHILE @n <= @totalStudents
    BEGIN
        SET @fn   = (SELECT TOP 1 Name FROM #firstName ORDER BY NEWID());
        SET @ln   = (SELECT TOP 1 Name FROM #lastName  ORDER BY NEWID());
        SET @prog = (SELECT TOP 1 Id   FROM #progIds   ORDER BY NEWID());
        SET @gender = CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 'Male' ELSE 'Female' END;
        SET @yob = DATEADD(DAY, -1 * (18 * 365 + ABS(CHECKSUM(NEWID())) % (5 * 365)), CAST(GETDATE() AS DATE));
        SET @enrol = DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % (5 * 365)), CAST(GETDATE() AS DATE));
        SET @yos = 1 + (ABS(CHECKSUM(NEWID())) % 4);

        INSERT INTO uni.Students
            (AdmissionNumber, FirstName, LastName, Gender, DateOfBirth, Programme, ProgrammeId, YearOfStudy, EnrolmentDate, IsActive)
        VALUES (
            'KAB/' + RIGHT('0000' + CAST(@n AS NVARCHAR), 4) + '/' + CAST(YEAR(@enrol) AS NVARCHAR),
            @fn, @ln, @gender, @yob,
            (SELECT ProgrammeName FROM uni.Programmes WHERE ProgrammeId = @prog),
            @prog, @yos, @enrol,
            CASE WHEN ABS(CHECKSUM(NEWID())) % 10 < 9 THEN 1 ELSE 0 END
        );

        SET @n = @n + 1;
    END

    DROP TABLE #firstName, #lastName, #progIds;
END
GO

-- ============================================
-- 5. Enrolments (~20000)
-- ============================================
IF (SELECT COUNT(*) FROM uni.Enrolments) < 1000
BEGIN
    DECLARE @k INT = 1;
    DECLARE @sid INT, @cid INT, @sem INT, @enrolDate DATE;

    WHILE @k <= 20000
    BEGIN
        SET @sid = (SELECT TOP 1 StudentId FROM uni.Students ORDER BY NEWID());
        SET @cid = (SELECT TOP 1 CourseId  FROM uni.Courses  WHERE ProgrammeId = (SELECT ProgrammeId FROM uni.Students WHERE StudentId = @sid) ORDER BY NEWID());
        SET @sem = (SELECT TOP 1 SemesterId FROM uni.Semesters ORDER BY NEWID());
        SET @enrolDate = (SELECT StartDate FROM uni.Semesters WHERE SemesterId = @sem);

        IF @cid IS NOT NULL
        BEGIN
            INSERT INTO uni.Enrolments (StudentId, CourseId, SemesterId, EnrolDate, Status)
            VALUES (@sid, @cid, @sem, @enrolDate,
                CASE WHEN ABS(CHECKSUM(NEWID())) % 20 < 19 THEN 'Active' ELSE 'Dropped' END);
        END
        SET @k = @k + 1;
    END
END
GO

-- ============================================
-- 6. Invoices (~15000)
-- ============================================
IF (SELECT COUNT(*) FROM uni.Invoices) < 1000
BEGIN
    DECLARE @m INT = 1;
    DECLARE @sid2 INT, @sem2 INT, @invDate DATE, @amt DECIMAL(12,2);

    WHILE @m <= 15000
    BEGIN
        SET @sid2 = (SELECT TOP 1 StudentId FROM uni.Students ORDER BY NEWID());
        SET @sem2 = (SELECT TOP 1 SemesterId FROM uni.Semesters ORDER BY NEWID());
        SET @invDate = (SELECT StartDate FROM uni.Semesters WHERE SemesterId = @sem2);
        SET @amt = 30000 + (ABS(CHECKSUM(NEWID())) % 70000);

        INSERT INTO uni.Invoices (StudentId, SemesterId, InvoiceDate, Description, Amount)
        VALUES (@sid2, @sem2, @invDate, 'Tuition and fees', @amt);
        SET @m = @m + 1;
    END
END
GO

-- ============================================
-- 7. Payments (~25000)
-- ============================================
IF (SELECT COUNT(*) FROM uni.Payments) < 1000
BEGIN
    IF OBJECT_ID('tempdb..#methods') IS NOT NULL DROP TABLE #methods;
    CREATE TABLE #methods (M NVARCHAR(20));
    INSERT INTO #methods (M) VALUES ('MPESA'),('Bank'),('Cash'),('Cheque'),('Card');

    DECLARE @p INT = 1;
    DECLARE @sid3 INT, @payDate DATE, @payAmt DECIMAL(12,2), @method NVARCHAR(20);

    WHILE @p <= 25000
    BEGIN
        SET @sid3 = (SELECT TOP 1 StudentId FROM uni.Students ORDER BY NEWID());
        SET @payDate = DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % (5 * 365)), CAST(GETDATE() AS DATE));
        SET @payAmt = 1000 + (ABS(CHECKSUM(NEWID())) % 25000);
        SET @method = (SELECT TOP 1 M FROM #methods ORDER BY NEWID());

        INSERT INTO uni.Payments (StudentId, PaymentDate, Amount, Method, Reference)
        VALUES (@sid3, @payDate, @payAmt, @method, 'REF' + RIGHT('000000' + CAST(@p AS NVARCHAR), 6));
        SET @p = @p + 1;
    END

    DROP TABLE #methods;
END
GO

-- ============================================
-- 8. Results (~20000)
-- ============================================
IF (SELECT COUNT(*) FROM uni.Results) < 1000
BEGIN
    DECLARE @r INT = 1;
    DECLARE @sid4 INT, @cid4 INT, @sem4 INT;
    DECLARE @score DECIMAL(5,2), @grade NVARCHAR(5);

    WHILE @r <= 20000
    BEGIN
        SET @sid4 = (SELECT TOP 1 StudentId FROM uni.Students ORDER BY NEWID());
        SET @cid4 = (SELECT TOP 1 CourseId  FROM uni.Courses ORDER BY NEWID());
        SET @sem4 = (SELECT TOP 1 SemesterId FROM uni.Semesters ORDER BY NEWID());
        SET @score = 30 + (ABS(CHECKSUM(NEWID())) % 70);
        SET @grade = CASE
            WHEN @score >= 70 THEN 'A'
            WHEN @score >= 60 THEN 'B'
            WHEN @score >= 50 THEN 'C'
            WHEN @score >= 40 THEN 'D'
            ELSE 'E' END;

        INSERT INTO uni.Results (StudentId, CourseId, SemesterId, Score, Grade)
        VALUES (@sid4, @cid4, @sem4, @score, @grade);
        SET @r = @r + 1;
    END
END
GO

-- ============================================
-- 9. Employees (~300)
-- ============================================
IF (SELECT COUNT(*) FROM uni.Employees) < 100
BEGIN
    IF OBJECT_ID('tempdb..#fn2') IS NOT NULL DROP TABLE #fn2;
    IF OBJECT_ID('tempdb..#ln2') IS NOT NULL DROP TABLE #ln2;
    IF OBJECT_ID('tempdb..#depts') IS NOT NULL DROP TABLE #depts;

    CREATE TABLE #fn2 (Name NVARCHAR(50));
    CREATE TABLE #ln2 (Name NVARCHAR(50));
    CREATE TABLE #depts (Name NVARCHAR(100));

    INSERT INTO #fn2 (Name) VALUES
        ('John'),('Mary'),('Peter'),('Grace'),('David'),('Faith'),('James'),('Mercy'),
        ('Samuel'),('Joyce'),('Daniel'),('Esther'),('Michael'),('Rose'),('Paul');

    INSERT INTO #ln2 (Name) VALUES
        ('Kimani'),('Wanjiku'),('Otieno'),('Cheruiyot'),('Kipchoge'),('Mwangi'),
        ('Odhiambo'),('Kariuki'),('Njuguna'),('Ochieng'),('Mutua'),('Wafula');

    INSERT INTO #depts (Name) SELECT DepartmentName FROM app.Departments;

    DECLARE @e INT = 1;
    DECLARE @fn2 NVARCHAR(50), @ln2 NVARCHAR(50), @dept NVARCHAR(100);
    DECLARE @pos NVARCHAR(50), @sal DECIMAL(12,2), @hd DATE, @act BIT;

    WHILE @e <= 300
    BEGIN
        SET @fn2  = (SELECT TOP 1 Name FROM #fn2   ORDER BY NEWID());
        SET @ln2  = (SELECT TOP 1 Name FROM #ln2   ORDER BY NEWID());
        SET @dept = (SELECT TOP 1 Name FROM #depts ORDER BY NEWID());
        SET @pos  = CASE ABS(CHECKSUM(NEWID())) % 5
                        WHEN 0 THEN 'Lecturer' WHEN 1 THEN 'Senior Lecturer'
                        WHEN 2 THEN 'Tutor' WHEN 3 THEN 'Administrator'
                        ELSE 'Support Staff' END;
        SET @hd   = DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % (15 * 365)), CAST(GETDATE() AS DATE));
        SET @sal  = 50000 + (ABS(CHECKSUM(NEWID())) % 200000);
        SET @act  = CASE WHEN ABS(CHECKSUM(NEWID())) % 10 < 9 THEN 1 ELSE 0 END;

        INSERT INTO uni.Employees (StaffNumber, FirstName, LastName, Department, Position, HireDate, Salary, IsActive)
        VALUES ('EMP' + RIGHT('0000' + CAST(@e AS NVARCHAR), 4), @fn2, @ln2, @dept, @pos, @hd, @sal, @act);

        SET @e = @e + 1;
    END

    DROP TABLE #fn2, #ln2, #depts;
END
GO

PRINT 'Large dataset seeded.';
GO

-- ============================================
-- Verification
-- ============================================
SELECT 'Programmes' AS Tbl, COUNT(*) AS Cnt FROM uni.Programmes
UNION ALL SELECT 'Semesters',  COUNT(*) FROM uni.Semesters
UNION ALL SELECT 'Courses',    COUNT(*) FROM uni.Courses
UNION ALL SELECT 'Students',   COUNT(*) FROM uni.Students
UNION ALL SELECT 'Enrolments', COUNT(*) FROM uni.Enrolments
UNION ALL SELECT 'Invoices',   COUNT(*) FROM uni.Invoices
UNION ALL SELECT 'Payments',   COUNT(*) FROM uni.Payments
UNION ALL SELECT 'Results',    COUNT(*) FROM uni.Results
UNION ALL SELECT 'Employees',  COUNT(*) FROM uni.Employees;
GO