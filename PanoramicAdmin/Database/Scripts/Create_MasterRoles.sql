USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_master_roles', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_master_roles]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Roles] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_pano_master_roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.pano_master_roles', N'U')
      AND name = N'UX_pano_master_roles_Roles'
)
BEGIN
    CREATE UNIQUE INDEX [UX_pano_master_roles_Roles]
        ON [dbo].[pano_master_roles] ([Roles]);
END;

IF OBJECT_ID(N'dbo.pano_master_pic', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.pano_master_pic', N'U')
         AND name = N'Role'
   )
BEGIN
    INSERT INTO [dbo].[pano_master_roles] ([Roles])
    SELECT DISTINCT LTRIM(RTRIM(pic.[Role]))
    FROM [dbo].[pano_master_pic] AS pic
    WHERE NULLIF(LTRIM(RTRIM(pic.[Role])), N'') IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[pano_master_roles] AS masterRole
          WHERE masterRole.[Roles] = LTRIM(RTRIM(pic.[Role]))
      );
END;
