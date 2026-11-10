USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_scrum_sprint', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_scrum_sprint]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Focus] nvarchar(250) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_pano_scrum_sprint] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_pano_scrum_sprint_DateRange] CHECK ([EndDate] >= [StartDate]),
        CONSTRAINT [CK_pano_scrum_sprint_Status]
            CHECK ([Status] IN (N'Direncanakan', N'Berjalan', N'Selesai'))
    );

    CREATE INDEX [IX_pano_scrum_sprint_Status]
        ON [dbo].[pano_scrum_sprint] ([Status]);
END;

IF OBJECT_ID(N'dbo.pano_scrum_task', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_scrum_task]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [SprintId] int NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Owner] nvarchar(150) NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [DueDate] date NULL,
        CONSTRAINT [PK_pano_scrum_task] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_pano_scrum_task_Sprint]
            FOREIGN KEY ([SprintId]) REFERENCES [dbo].[pano_scrum_sprint] ([Id]),
        CONSTRAINT [CK_pano_scrum_task_Priority]
            CHECK ([Priority] IN (N'Rendah', N'Sedang', N'Tinggi')),
        CONSTRAINT [CK_pano_scrum_task_Status]
            CHECK ([Status] IN (N'Belum dimulai', N'Sedang dikerjakan', N'Dalam review', N'Selesai'))
    );

    CREATE INDEX [IX_pano_scrum_task_Sprint_Status]
        ON [dbo].[pano_scrum_task] ([SprintId], [Status]);
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
