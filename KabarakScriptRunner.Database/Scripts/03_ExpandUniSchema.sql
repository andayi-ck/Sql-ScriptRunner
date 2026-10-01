USE KabarakScriptRunner;
GO

-- Programmes
IF OBJECT_ID('uni.Programmes', 'U') IS NULL
CREATE TABLE uni.Programmes (
    ProgrammeId    INT IDENTITY PRIMARY KEY,
    ProgrammeCode  NVARCHAR(20)  NOT NULL,
    ProgrammeName  NVARCHAR(150) NOT NULL,
    School         NVARCHAR(100) NOT NULL,  -- e.g. 'School of Science'
    DurationYears  INT NOT NULL DEFAULT 4,
    IsActive       BIT NOT NULL DEFAULT 1
);
GO

-- Academic years / semesters
IF OBJECT_ID('uni.Semesters', 'U') IS NULL
CREATE TABLE uni.Semesters (
    SemesterId   INT IDENTITY PRIMARY KEY,
    SemesterName NVARCHAR(50)  NOT NULL,   -- e.g. '2026 Semester 1'
    StartDate    DATE NOT NULL,
    EndDate      DATE NOT NULL,
    IsActive     BIT NOT NULL DEFAULT 1
);
GO

-- Courses
IF OBJECT_ID('uni.Courses', 'U') IS NULL
CREATE TABLE uni.Courses (
    CourseId    INT IDENTITY PRIMARY KEY,
    CourseCode  NVARCHAR(20)  NOT NULL,
    CourseName  NVARCHAR(150) NOT NULL,
    Credits     INT NOT NULL DEFAULT 3,
    ProgrammeId INT NULL,
    FOREIGN KEY (ProgrammeId) REFERENCES uni.Programmes(ProgrammeId)
);
GO

-- Expand Students (add columns)
IF COL_LENGTH('uni.Students', 'AdmissionNumber') IS NULL
    ALTER TABLE uni.Students ADD AdmissionNumber NVARCHAR(30) NULL;
IF COL_LENGTH('uni.Students', 'Gender') IS NULL
    ALTER TABLE uni.Students ADD Gender NVARCHAR(10) NULL;
IF COL_LENGTH('uni.Students', 'DateOfBirth') IS NULL
    ALTER TABLE uni.Students ADD DateOfBirth DATE NULL;
IF COL_LENGTH('uni.Students', 'ProgrammeId') IS NULL
    ALTER TABLE uni.Students ADD ProgrammeId INT NULL;
IF COL_LENGTH('uni.Students', 'YearOfStudy') IS NULL
    ALTER TABLE uni.Students ADD YearOfStudy INT NULL;
IF COL_LENGTH('uni.Students', 'IsActive') IS NULL
    ALTER TABLE uni.Students ADD IsActive BIT NOT NULL DEFAULT 1;
GO

-- Enrolments (student ↔ course per semester)
IF OBJECT_ID('uni.Enrolments', 'U') IS NULL
CREATE TABLE uni.Enrolments (
    EnrolmentId INT IDENTITY PRIMARY KEY,
    StudentId   INT NOT NULL,
    CourseId    INT NOT NULL,
    SemesterId  INT NOT NULL,
    EnrolDate   DATE NOT NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Active',
    FOREIGN KEY (StudentId)  REFERENCES uni.Students(StudentId),
    FOREIGN KEY (CourseId)   REFERENCES uni.Courses(CourseId),
    FOREIGN KEY (SemesterId) REFERENCES uni.Semesters(SemesterId)
);
GO

-- Invoices (fees billed to students)
IF OBJECT_ID('uni.Invoices', 'U') IS NULL
CREATE TABLE uni.Invoices (
    InvoiceId    INT IDENTITY PRIMARY KEY,
    StudentId    INT NOT NULL,
    SemesterId   INT NOT NULL,
    InvoiceDate  DATE NOT NULL,
    Description  NVARCHAR(200) NULL,
    Amount       DECIMAL(12,2) NOT NULL,
    FOREIGN KEY (StudentId)  REFERENCES uni.Students(StudentId),
    FOREIGN KEY (SemesterId) REFERENCES uni.Semesters(SemesterId)
);
GO

-- Payments (fees paid)
IF OBJECT_ID('uni.Payments', 'U') IS NULL
CREATE TABLE uni.Payments (
    PaymentId    INT IDENTITY PRIMARY KEY,
    StudentId    INT NOT NULL,
    PaymentDate  DATE NOT NULL,
    Amount       DECIMAL(12,2) NOT NULL,
    Method       NVARCHAR(30) NULL,  -- MPESA, Bank, Cash, Cheque
    Reference    NVARCHAR(50) NULL,
    FOREIGN KEY (StudentId) REFERENCES uni.Students(StudentId)
);
GO

-- Exam results
IF OBJECT_ID('uni.Results', 'U') IS NULL
CREATE TABLE uni.Results (
    ResultId   INT IDENTITY PRIMARY KEY,
    StudentId  INT NOT NULL,
    CourseId   INT NOT NULL,
    SemesterId INT NOT NULL,
    Score      DECIMAL(5,2) NULL,      -- 0-100
    Grade      NVARCHAR(5) NULL,       -- A, B+, C, etc.
    FOREIGN KEY (StudentId)  REFERENCES uni.Students(StudentId),
    FOREIGN KEY (CourseId)   REFERENCES uni.Courses(CourseId),
    FOREIGN KEY (SemesterId) REFERENCES uni.Semesters(SemesterId)
);
GO

-- Employees (staff) — for HR reports
IF OBJECT_ID('uni.Employees', 'U') IS NULL
CREATE TABLE uni.Employees (
    EmployeeId   INT IDENTITY PRIMARY KEY,
    StaffNumber  NVARCHAR(20) NOT NULL,
    FirstName    NVARCHAR(100) NOT NULL,
    LastName     NVARCHAR(100) NOT NULL,
    Department   NVARCHAR(100) NULL,
    Position     NVARCHAR(100) NULL,
    HireDate     DATE NULL,
    Salary       DECIMAL(12,2) NULL,
    IsActive     BIT NOT NULL DEFAULT 1
);
GO

PRINT 'uni schema expanded.';
GO