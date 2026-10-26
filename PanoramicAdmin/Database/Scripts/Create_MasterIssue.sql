USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_master_issue', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_master_issue]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Issue] nvarchar(200) NOT NULL,
        [Active] bit NOT NULL
            CONSTRAINT [DF_pano_master_issue_Active] DEFAULT (1),
        CONSTRAINT [PK_pano_master_issue] PRIMARY KEY ([Id])
    );
END;
