USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_strategic_action', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_strategic_action]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [NamaStrategicAction] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_pano_strategic_action] PRIMARY KEY ([Id])
    );
END;
