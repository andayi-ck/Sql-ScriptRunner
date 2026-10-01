USE KabarakScriptRunner;
GO

-- ============================================
-- 1. Departments table
-- ============================================
IF OBJECT_ID('app.Departments', 'U') IS NULL
BEGIN
    CREATE TABLE app.Departments (
        DepartmentId   INT IDENTITY PRIMARY KEY,
        DepartmentName NVARCHAR(100) NOT NULL UNIQUE,
        IsActive       BIT NOT NULL DEFAULT 1
    );

    INSERT INTO app.Departments (DepartmentName) VALUES
        ('Registry'),
        ('Finance'),
        ('Examinations'),
        ('Human Resources'),
        ('ICT'),
        ('Administration');
END
GO

-- ============================================
-- 2. Add DepartmentId to Users
-- ============================================
IF COL_LENGTH('app.Users', 'DepartmentId') IS NULL
BEGIN
    ALTER TABLE app.Users
    ADD DepartmentId INT NULL;

    ALTER TABLE app.Users
    ADD CONSTRAINT FK_Users_Departments
    FOREIGN KEY (DepartmentId) REFERENCES app.Departments(DepartmentId);
END
GO

-- ============================================
-- 3. Add DepartmentId to Scripts
-- ============================================
IF COL_LENGTH('app.Scripts', 'DepartmentId') IS NULL
BEGIN
    ALTER TABLE app.Scripts
    ADD DepartmentId INT NULL;

    ALTER TABLE app.Scripts
    ADD CONSTRAINT FK_Scripts_Departments
    FOREIGN KEY (DepartmentId) REFERENCES app.Departments(DepartmentId);
END
GO

-- ============================================
-- 4. Assign existing data to sensible departments
-- ============================================
-- Put 'admin' in Administration
UPDATE app.Users
SET DepartmentId = (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Administration')
WHERE UserId = 'admin' AND DepartmentId IS NULL;

-- Put 'jdoe' in Registry
UPDATE app.Users
SET DepartmentId = (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
WHERE UserId = 'jdoe' AND DepartmentId IS NULL;

-- Put the sample script in Registry
UPDATE app.Scripts
SET DepartmentId = (SELECT DepartmentId FROM app.Departments WHERE DepartmentName = 'Registry')
WHERE DepartmentId IS NULL;
GO

-- ============================================
-- 5. Verify
-- ============================================
SELECT * FROM app.Departments;
SELECT UserId, DisplayName, IsAdmin, DepartmentId FROM app.Users;
SELECT ScriptId, Name, DepartmentId FROM app.Scripts;
GO
