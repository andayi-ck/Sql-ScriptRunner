-- ============================================
-- Kabarak Script Runner — Initial Schema Setup
-- Run this ONCE against a fresh SQL Server instance.
-- ============================================

-- 1. Create the database (skip if it already exists)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'KabarakScriptRunner')
BEGIN
    CREATE DATABASE KabarakScriptRunner;
END
GO

USE KabarakScriptRunner;
GO

-- 2. Create schemas (safe: skip if they exist)
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'app')
    EXEC('CREATE SCHEMA app;');
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'uni')
    EXEC('CREATE SCHEMA uni;');
GO

-- 3. App schema tables
IF OBJECT_ID('app.Users', 'U') IS NULL
BEGIN
    CREATE TABLE app.Users (
        UserId       NVARCHAR(50)  NOT NULL PRIMARY KEY,
        DisplayName  NVARCHAR(150) NOT NULL,
        PasswordHash NVARCHAR(256) NOT NULL,
        IsAdmin      BIT           NOT NULL DEFAULT 0,
        IsActive     BIT           NOT NULL DEFAULT 1
    );
END
GO

IF OBJECT_ID('app.Scripts', 'U') IS NULL
BEGIN
    CREATE TABLE app.Scripts (
        ScriptId      INT IDENTITY PRIMARY KEY,
        Name          NVARCHAR(150) NOT NULL,
        Description   NVARCHAR(400) NULL,
        SqlText       NVARCHAR(MAX) NOT NULL,
        Category      NVARCHAR(50)  NULL,
        NeedsDateFrom BIT NOT NULL DEFAULT 1,
        NeedsDateTo   BIT NOT NULL DEFAULT 1,
        IsActive      BIT NOT NULL DEFAULT 1
    );
END
GO

IF OBJECT_ID('app.UserScripts', 'U') IS NULL
BEGIN
    CREATE TABLE app.UserScripts (
        UserId   NVARCHAR(50) NOT NULL,
        ScriptId INT          NOT NULL,
        PRIMARY KEY (UserId, ScriptId),
        FOREIGN KEY (UserId)   REFERENCES app.Users(UserId),
        FOREIGN KEY (ScriptId) REFERENCES app.Scripts(ScriptId)
    );
END
GO

IF OBJECT_ID('app.ScriptRuns', 'U') IS NULL
BEGIN
    CREATE TABLE app.ScriptRuns (
        RunId        BIGINT IDENTITY PRIMARY KEY,
        UserId       NVARCHAR(50) NOT NULL,
        ScriptId     INT          NOT NULL,
        StartedAt    DATETIME2    NOT NULL,
        FinishedAt   DATETIME2    NULL,
        Status       NVARCHAR(20) NOT NULL,
        RowsAffected     INT          NULL,
        OutputPath   NVARCHAR(400) NULL,
        DateFrom     DATE NULL,
        DateTo       DATE NULL,
        ErrorMessage NVARCHAR(MAX) NULL
    );
END
GO

IF OBJECT_ID('app.Settings', 'U') IS NULL
BEGIN
    CREATE TABLE app.Settings (
        [Key]   NVARCHAR(80) PRIMARY KEY,
        [Value] NVARCHAR(400) NOT NULL
    );
END
GO

-- 4. Slim university tables (Phase 1 — will grow later)
IF OBJECT_ID('uni.Students', 'U') IS NULL
BEGIN
    CREATE TABLE uni.Students (
        StudentId     INT IDENTITY PRIMARY KEY,
        FirstName     NVARCHAR(100),
        LastName      NVARCHAR(100),
        Programme     NVARCHAR(100),
        EnrolmentDate DATE
    );
END
GO

-- 5. Seed default admin user (PasswordHash is a placeholder for now)
IF NOT EXISTS (SELECT 1 FROM app.Users WHERE UserId = 'admin')
BEGIN
    INSERT INTO app.Users (UserId, DisplayName, PasswordHash, IsAdmin)
    VALUES ('admin', 'System Administrator', 'PLACEHOLDER_HASH', 1);
END
GO

-- 6. Seed one sample script
IF NOT EXISTS (SELECT 1 FROM app.Scripts WHERE Name = 'Student Enrolment Report')
BEGIN
    INSERT INTO app.Scripts (Name, Description, SqlText, Category, NeedsDateFrom, NeedsDateTo)
    VALUES (
        'Student Enrolment Report',
        'Lists all students enrolled in a given date range',
        'SELECT StudentId, FirstName, LastName, Programme, EnrolmentDate
         FROM uni.Students
         WHERE EnrolmentDate BETWEEN @DateFrom AND @DateTo',
        'Registry',
        1,
        1
    );
END
GO

-- 7. Assign the script to admin
IF NOT EXISTS (
    SELECT 1 FROM app.UserScripts
    WHERE UserId = 'admin' AND ScriptId = (SELECT TOP 1 ScriptId FROM app.Scripts WHERE Name = 'Student Enrolment Report')
)
BEGIN
    INSERT INTO app.UserScripts (UserId, ScriptId)
    SELECT 'admin', ScriptId FROM app.Scripts WHERE Name = 'Student Enrolment Report';
END
GO

-- 8. Seed dummy student data
IF NOT EXISTS (SELECT 1 FROM uni.Students)
BEGIN
    INSERT INTO uni.Students (FirstName, LastName, Programme, EnrolmentDate)
    VALUES
        ('John',  'Doe',     'BSc Computer Science',        '2026-09-01'),
        ('Jane',  'Smith',   'BSc Information Technology',  '2026-09-05'),
        ('Alice', 'Johnson', 'BBA Finance',                 '2026-09-10');
END
GO

-- 9. Seed default settings
IF NOT EXISTS (SELECT 1 FROM app.Settings WHERE [Key] = 'OutputFolder')
BEGIN
    INSERT INTO app.Settings ([Key], [Value]) VALUES ('OutputFolder', 'C:\KabarakExports');
END
GO

PRINT 'Database KabarakScriptRunner initialized successfully.';
GO