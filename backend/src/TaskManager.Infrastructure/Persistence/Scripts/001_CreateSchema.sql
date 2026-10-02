-- Idempotent schema script. Executed automatically on API startup (Database:InitializeOnStartup)
-- and safe to run manually from SSMS against the TaskManager database.

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        -- Explicit case-insensitive collation: "Francisco" and "francisco" must be the same user
        -- regardless of the server's default collation.
        Username     NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
        Email        NVARCHAR(254)  COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
        PasswordHash NVARCHAR(512)  NOT NULL,
        CreatedAtUtc DATETIME2(7)   NOT NULL,
        CONSTRAINT UQ_Users_Username UNIQUE (Username),
        CONSTRAINT UQ_Users_Email UNIQUE (Email)
    );
END;

IF OBJECT_ID(N'dbo.Tasks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tasks
    (
        -- Random GUIDs make a poor clustered key (page splits), so the PK is non-clustered
        -- and the table is clustered by the access pattern: "my tasks ordered by due date".
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tasks PRIMARY KEY NONCLUSTERED,
        UserId       UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT FK_Tasks_Users REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        Title        NVARCHAR(200)  NOT NULL,
        Description  NVARCHAR(2000) NULL,
        Status       TINYINT        NOT NULL CONSTRAINT CK_Tasks_Status CHECK (Status IN (0, 1, 2)),
        DueDate      DATE           NOT NULL,
        CreatedAtUtc DATETIME2(7)   NOT NULL,
        UpdatedAtUtc DATETIME2(7)   NOT NULL,
        CONSTRAINT CK_Tasks_Title_NotBlank CHECK (LEN(Title) > 0)
    );

    CREATE CLUSTERED INDEX CIX_Tasks_UserId_DueDate ON dbo.Tasks (UserId, DueDate);
END;
