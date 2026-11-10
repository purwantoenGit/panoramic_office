USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_scrum_product_backlog_item', N'U') IS NULL
BEGIN
    ;THROW 51000, 'Run Create_ProductBacklogItem.sql before Create_ScrumProject.sql.', 1;
END;

IF OBJECT_ID(N'dbo.pano_scrum_sprint', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_scrum_sprint]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ProductBacklogItemId] int NULL,
        [Focus] nvarchar(250) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_pano_scrum_sprint] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_pano_scrum_sprint_ProductBacklogItem]
            FOREIGN KEY ([ProductBacklogItemId])
            REFERENCES [dbo].[pano_scrum_product_backlog_item] ([Id]),
        CONSTRAINT [CK_pano_scrum_sprint_DateRange] CHECK ([EndDate] >= [StartDate]),
        CONSTRAINT [CK_pano_scrum_sprint_Status]
            CHECK ([Status] IN (N'Direncanakan', N'Berjalan', N'Selesai'))
    );

    CREATE INDEX [IX_pano_scrum_sprint_Status]
        ON [dbo].[pano_scrum_sprint] ([Status]);
END;

IF COL_LENGTH(N'dbo.pano_scrum_sprint', N'ProductBacklogItemId') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_scrum_sprint]
        ADD [ProductBacklogItemId] int NULL;
END;

IF OBJECT_ID(N'dbo.FK_pano_scrum_sprint_ProductBacklogItem', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_scrum_sprint]
        ADD CONSTRAINT [FK_pano_scrum_sprint_ProductBacklogItem]
            FOREIGN KEY ([ProductBacklogItemId])
            REFERENCES [dbo].[pano_scrum_product_backlog_item] ([Id]);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.pano_scrum_sprint')
      AND [name] = N'IX_pano_scrum_sprint_ProductBacklogItem'
)
BEGIN
    CREATE INDEX [IX_pano_scrum_sprint_ProductBacklogItem]
        ON [dbo].[pano_scrum_sprint] ([ProductBacklogItemId]);
END;

IF OBJECT_ID(N'dbo.pano_scrum_task', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_scrum_task]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [SprintId] int NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Owner] nvarchar(150) NULL,
        [Category] nvarchar(30) NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [EstimatedHour] decimal(8,2) NULL,
        [Notes] nvarchar(max) NULL,
        CONSTRAINT [PK_pano_scrum_task] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_pano_scrum_task_Sprint]
            FOREIGN KEY ([SprintId]) REFERENCES [dbo].[pano_scrum_sprint] ([Id]),
        CONSTRAINT [CK_pano_scrum_task_Category]
            CHECK ([Category] IS NULL OR [Category] IN
                (N'Backend', N'Change Management', N'Dashboard', N'Dokumentasi',
                 N'Frontend', N'Hardware', N'QA', N'UR')),
        CONSTRAINT [CK_pano_scrum_task_EstimatedHour]
            CHECK ([EstimatedHour] IS NULL OR [EstimatedHour] >= 0),
        CONSTRAINT [CK_pano_scrum_task_Priority]
            CHECK ([Priority] IN (N'Rendah', N'Sedang', N'Tinggi')),
        CONSTRAINT [CK_pano_scrum_task_Status]
            CHECK ([Status] IN (N'Belum dimulai', N'Sedang dikerjakan', N'Dalam review', N'Selesai'))
    );

    CREATE INDEX [IX_pano_scrum_task_Sprint_Status]
        ON [dbo].[pano_scrum_task] ([SprintId], [Status]);
END;

IF COL_LENGTH(N'dbo.pano_scrum_task', N'Category') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_scrum_task] ADD [Category] nvarchar(30) NULL;
END;

IF COL_LENGTH(N'dbo.pano_scrum_task', N'EstimatedHour') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_scrum_task] ADD [EstimatedHour] decimal(8,2) NULL;
END;

IF COL_LENGTH(N'dbo.pano_scrum_task', N'Notes') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_scrum_task] ADD [Notes] nvarchar(max) NULL;
END;

IF OBJECT_ID(N'dbo.CK_pano_scrum_task_Category', N'C') IS NULL
BEGIN
    EXEC(N'ALTER TABLE [dbo].[pano_scrum_task]
        ADD CONSTRAINT [CK_pano_scrum_task_Category]
        CHECK ([Category] IS NULL OR [Category] IN
            (N''Backend'', N''Change Management'', N''Dashboard'', N''Dokumentasi'',
             N''Frontend'', N''Hardware'', N''QA'', N''UR''));');
END;

IF OBJECT_ID(N'dbo.CK_pano_scrum_task_EstimatedHour', N'C') IS NULL
BEGIN
    EXEC(N'ALTER TABLE [dbo].[pano_scrum_task]
        ADD CONSTRAINT [CK_pano_scrum_task_EstimatedHour]
        CHECK ([EstimatedHour] IS NULL OR [EstimatedHour] >= 0);');
END;

IF OBJECT_ID(N'dbo.pano_scrum_blocker', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_scrum_blocker]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [SprintId] int NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Detail] nvarchar(max) NOT NULL,
        [Owner] nvarchar(150) NULL,
        [NextStep] nvarchar(max) NOT NULL,
        [IsResolved] bit NOT NULL CONSTRAINT [DF_pano_scrum_blocker_IsResolved] DEFAULT (0),
        CONSTRAINT [PK_pano_scrum_blocker] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_pano_scrum_blocker_Sprint]
            FOREIGN KEY ([SprintId]) REFERENCES [dbo].[pano_scrum_sprint] ([Id])
    );

    CREATE INDEX [IX_pano_scrum_blocker_Sprint_Resolved]
        ON [dbo].[pano_scrum_blocker] ([SprintId], [IsResolved]);
END;
