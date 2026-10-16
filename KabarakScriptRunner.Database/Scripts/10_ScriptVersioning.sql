USE KabarakScriptRunner;
GO

-- ============================================
-- 1. Add Version column to app.Scripts
-- ============================================
IF COL_LENGTH('app.Scripts', 'Version') IS NULL
BEGIN
    ALTER TABLE app.Scripts
    ADD Version INT NOT NULL DEFAULT 1;

    PRINT 'app.Scripts.Version column added.';
END
ELSE
BEGIN
    PRINT 'app.Scripts.Version already exists.';
END
GO

-- ============================================
-- 2. Create app.ScriptVersions table
-- ============================================
IF OBJECT_ID('app.ScriptVersions', 'U') IS NULL
BEGIN
    CREATE TABLE app.ScriptVersions (
        VersionId     BIGINT IDENTITY PRIMARY KEY,
        ScriptId      INT           NOT NULL,           -- FK to app.Scripts
        Version       INT           NOT NULL,
        Name          NVARCHAR(150) NOT NULL,
        Description   NVARCHAR(400) NULL,
        SqlText       NVARCHAR(MAX) NOT NULL,
        Category      NVARCHAR(50)  NULL,
        DepartmentId  INT           NULL,
        NeedsDateFrom BIT           NOT NULL,
        NeedsDateTo   BIT           NOT NULL,
        IsActive      BIT           NOT NULL,
        ArchivedAt    DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
        ArchivedBy    NVARCHAR(50)  NULL                -- who triggered the change
    );

    CREATE INDEX IX_ScriptVersions_ScriptId
        ON app.ScriptVersions (ScriptId, Version DESC);

    PRINT 'app.ScriptVersions table created.';
END
ELSE
BEGIN
    PRINT 'app.ScriptVersions already exists.';
END
GO

-- ============================================
-- 3. Trigger: archive old values on UPDATE
-- ============================================
IF OBJECT_ID('app.trg_Scripts_VersionOnUpdate', 'TR') IS NOT NULL
    DROP TRIGGER app.trg_Scripts_VersionOnUpdate;
GO

CREATE TRIGGER app.trg_Scripts_VersionOnUpdate
ON app.Scripts
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Archive the OLD values (from the deleted pseudo-table)
    INSERT INTO app.ScriptVersions (
        ScriptId, Version, Name, Description, SqlText, Category,
        DepartmentId, NeedsDateFrom, NeedsDateTo, IsActive, ArchivedBy
    )
    SELECT
        d.ScriptId,
        d.Version,              -- the version BEFORE the update
        d.Name,
        d.Description,
        d.SqlText,
        d.Category,
        d.DepartmentId,
        d.NeedsDateFrom,
        d.NeedsDateTo,
        d.IsActive,
        SUSER_SNAME()           -- the SQL login/user who performed the update
    FROM deleted d;

    -- Bump the version on the current row
    UPDATE s
    SET s.Version = d.Version + 1
    FROM app.Scripts s
    INNER JOIN deleted d ON s.ScriptId = d.ScriptId;

    PRINT 'Archived old script version and bumped version.';
END
GO

PRINT 'Script versioning setup complete.';
GO