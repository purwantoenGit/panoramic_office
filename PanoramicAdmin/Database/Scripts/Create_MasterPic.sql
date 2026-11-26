USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_master_pic', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_master_pic]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Nama] nvarchar(150) NOT NULL,
        [Role] nvarchar(100) NOT NULL
            CONSTRAINT [DF_pano_master_pic_Role] DEFAULT (N''),
        [PhoneNumber] nvarchar(16) NULL,
        [WhatsAppId] nvarchar(100) NULL,
        [Email] nvarchar(254) NULL,
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

IF COL_LENGTH(N'dbo.pano_master_pic', N'PhoneNumber') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_master_pic]
        ADD [PhoneNumber] nvarchar(16) NULL;
END;

IF COL_LENGTH(N'dbo.pano_master_pic', N'WhatsAppId') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_master_pic]
        ADD [WhatsAppId] nvarchar(100) NULL;
END;

IF COL_LENGTH(N'dbo.pano_master_pic', N'Email') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_master_pic]
        ADD [Email] nvarchar(254) NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.pano_master_pic')
      AND [name] = N'UX_pano_master_pic_PhoneNumber'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_pano_master_pic_PhoneNumber]
        ON [dbo].[pano_master_pic] ([PhoneNumber])
        WHERE [PhoneNumber] IS NOT NULL;');
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.pano_master_pic')
      AND [name] = N'UX_pano_master_pic_WhatsAppId'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_pano_master_pic_WhatsAppId]
        ON [dbo].[pano_master_pic] ([WhatsAppId])
        WHERE [WhatsAppId] IS NOT NULL;');
END;
