USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_master_pic', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_master_pic]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Nama] nvarchar(150) NOT NULL,
        [Role] nvarchar(100) NOT NULL
            CONSTRAINT [DF_pano_master_pic_Role] DEFAULT (N''),
        CONSTRAINT [PK_pano_master_pic] PRIMARY KEY ([Id])
    );
END;

IF OBJECT_ID(N'dbo.pano_master_pic', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.pano_master_pic', N'U')
         AND name = N'Role'
   )
BEGIN
    ALTER TABLE [dbo].[pano_master_pic]
        ADD [Role] nvarchar(100) NOT NULL
            CONSTRAINT [DF_pano_master_pic_Role] DEFAULT (N'');
END;
