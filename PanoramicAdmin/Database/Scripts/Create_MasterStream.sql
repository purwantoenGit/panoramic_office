USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_master_stream', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_master_stream]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [NamaStream] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_pano_master_stream] PRIMARY KEY ([Id])
    );
END;
