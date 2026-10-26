USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_master_sites', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_master_sites]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Sites] nvarchar(150) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_pano_master_sites] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_pano_master_sites_Status]
            CHECK ([Status] COLLATE Latin1_General_100_BIN2 IN (N'Active', N'Non Active'))
    );
END;
