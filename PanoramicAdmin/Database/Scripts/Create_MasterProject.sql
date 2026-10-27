USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_MasterProject', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_MasterProject]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Stream] nvarchar(100) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [LogoIcon] nvarchar(50) NOT NULL
            CONSTRAINT [DF_pano_MasterProject_LogoIcon] DEFAULT (N'folder'),
        CONSTRAINT [PK_MasterProject] PRIMARY KEY ([Id])
    );
END;

IF COL_LENGTH(N'dbo.pano_MasterProject', N'LogoIcon') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_MasterProject]
    ADD [LogoIcon] nvarchar(50) NOT NULL
        CONSTRAINT [DF_pano_MasterProject_LogoIcon] DEFAULT (N'folder');
END;

IF OBJECT_ID(N'dbo.pano_MasterProjectSite', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_MasterProjectSite]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ProjectId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_MasterProjectSite] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_MasterProjectSite_MasterProject_ProjectId'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pano_MasterProjectSite', N'U')
)
BEGIN
    ALTER TABLE [dbo].[pano_MasterProjectSite]
    ADD CONSTRAINT [FK_MasterProjectSite_MasterProject_ProjectId]
        FOREIGN KEY ([ProjectId])
        REFERENCES [dbo].[pano_MasterProject] ([Id])
        ON DELETE CASCADE;
END;
