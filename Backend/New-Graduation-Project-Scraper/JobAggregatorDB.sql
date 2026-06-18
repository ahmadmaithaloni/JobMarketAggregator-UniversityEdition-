IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [JobSites] (
    [SiteId] int NOT NULL IDENTITY,
    [SiteUrl] nvarchar(500) NOT NULL,
    [SiteName] nvarchar(100) NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_JobSites] PRIMARY KEY ([SiteId])
);

CREATE TABLE [Users] (
    [UserId] int NOT NULL IDENTITY,
    [UserName] nvarchar(250) NOT NULL,
    [UserAddress] nvarchar(500) NOT NULL,
    [UserEmail] nvarchar(100) NOT NULL,
    [UserPhone] varchar(15) NOT NULL,
    [UserMajor] varchar(100) NOT NULL,
    [UserPassword] nvarchar(256) NOT NULL,
    [CreationDate] datetime NOT NULL DEFAULT ((getdate())),
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserId])
);

CREATE TABLE [JobQueries] (
    [QueryId] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [QueryDescription] nvarchar(1000) NOT NULL,
    [CreationDate] datetime NOT NULL DEFAULT ((getdate())),
    [QJobName] nvarchar(200) NOT NULL DEFAULT N'Software Developer',
    [QJobLocation] nvarchar(150) NOT NULL DEFAULT N'Amman, Jordan',
    [QJobStartTime] time NOT NULL DEFAULT '09:00:00',
    [QJobEndTime] time NOT NULL DEFAULT '17:00:00',
    [QLowSalary] money NOT NULL,
    [QHighSalary] money NOT NULL,
    CONSTRAINT [PK_JobQueries] PRIMARY KEY ([QueryId]),
    CONSTRAINT [FK_JobQueries_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);

CREATE TABLE [ScrapedJobs] (
    [JobId] int NOT NULL IDENTITY,
    [JobName] nvarchar(200) NOT NULL,
    [JobLocation] nvarchar(250) NOT NULL,
    [JobUrl] nvarchar(500) NOT NULL,
    [SiteId] int NOT NULL,
    [JobDescription] nvarchar(max) NOT NULL,
    [JobSalary] nvarchar(100) NULL,
    [JobDatePosted] nvarchar(100) NULL,
    [JobNotes] nvarchar(1000) NULL,
    [IsAvailable] bit NOT NULL DEFAULT CAST(1 AS bit),
    [QueryId] int NOT NULL,
    CONSTRAINT [PK_ScrapedJobs] PRIMARY KEY ([JobId]),
    CONSTRAINT [FK_ScrapedJobs_JobQueries] FOREIGN KEY ([QueryId]) REFERENCES [JobQueries] ([QueryId]) ON DELETE CASCADE,
    CONSTRAINT [FK_ScrapedJobs_JobSites] FOREIGN KEY ([SiteId]) REFERENCES [JobSites] ([SiteId]) ON DELETE CASCADE
);

CREATE INDEX [IX_JobQueries_UserId] ON [JobQueries] ([UserId]);

CREATE INDEX [IX_ScrapedJobs_QueryId] ON [ScrapedJobs] ([QueryId]);

CREATE INDEX [IX_ScrapedJobs_SiteId] ON [ScrapedJobs] ([SiteId]);

CREATE INDEX [IX_Users_Email] ON [Users] ([UserEmail]);

CREATE UNIQUE INDEX [UQ_Users_Email] ON [Users] ([UserEmail]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260102010743_AddSalaryAndDate2', N'10.0.1');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ScrapedJobs]') AND [c].[name] = N'JobNotes');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [ScrapedJobs] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [ScrapedJobs] ALTER COLUMN [JobNotes] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260111213149_IncreaseNotesSize', N'10.0.1');

COMMIT;
GO

