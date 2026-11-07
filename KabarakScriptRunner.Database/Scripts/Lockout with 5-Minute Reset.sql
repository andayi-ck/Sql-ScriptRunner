USE KabarakScriptRunner;
GO

-- 1. Add the columns to track lockout
IF COL_LENGTH('app.Users', 'FailedAttempts') IS NULL
    ALTER TABLE app.Users ADD FailedAttempts INT NOT NULL DEFAULT 0;

IF COL_LENGTH('app.Users', 'LockoutEnd') IS NULL
    ALTER TABLE app.Users ADD LockoutEnd DATETIME2 NULL;
GO

-- 2. Reset existing users just in case
UPDATE app.Users SET FailedAttempts = 0, LockoutEnd = NULL;
GO