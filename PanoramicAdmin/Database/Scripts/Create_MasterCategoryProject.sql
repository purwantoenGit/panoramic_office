USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_master_category_project', N'U') IS NULL
   AND OBJECT_ID(N'dbo.pano_master_status', N'U') IS NOT NULL
BEGIN
    EXEC sys.sp_rename
        N'dbo.pano_master_status',
        N'pano_master_category_project';
END;

IF OBJECT_ID(N'dbo.pano_master_category_project', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_master_category_project]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [CategoryProject] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_pano_master_category_project] PRIMARY KEY ([Id])
    );
END;

IF OBJECT_ID(N'dbo.pano_master_category_project', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.pano_master_category_project')
         AND name = N'Status'
   )
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.pano_master_category_project')
         AND name = N'CategoryProject'
   )
BEGIN
    EXEC sys.sp_rename
        N'dbo.pano_master_category_project.Status',
        N'CategoryProject',
        N'COLUMN';
END;
