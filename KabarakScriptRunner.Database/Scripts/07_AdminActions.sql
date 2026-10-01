USE KabarakScriptRunner;
GO

IF OBJECT_ID('app.AdminActions', 'U') IS NULL
BEGIN
    CREATE TABLE app.AdminActions (
        ActionId    BIGINT IDENTITY PRIMARY KEY,
        UserId      NVARCHAR(50)  NOT NULL,       -- who performed it
        ActionType  NVARCHAR(50)  NOT NULL,       -- 'UserCreated', 'PasswordChanged', 'ScriptReassigned', etc.
        TargetType  NVARCHAR(50)  NULL,           -- 'User', 'Script', 'Setting'
        TargetId    NVARCHAR(100) NULL,           -- e.g. user ID, script ID
        Details     NVARCHAR(MAX) NULL,           -- human-readable description
        Timestamp   DATETIME2     NOT NULL DEFAULT SYSDATETIME()
    );
END
GO

PRINT 'app.AdminActions created.';
GO