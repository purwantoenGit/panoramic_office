USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_smart_refueling_curve_progress', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_smart_refueling_curve_progress]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Site] nvarchar(150) NOT NULL,
        [ItemKey] nvarchar(20) NOT NULL,
        [Percent] int NOT NULL,
        [UpdatedOn] date NOT NULL,
        CONSTRAINT [PK_pano_smart_refueling_curve_progress] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_pano_smart_refueling_curve_progress_Percent]
            CHECK ([Percent] >= 0 AND [Percent] <= 100)
    );

    CREATE UNIQUE INDEX [UX_pano_smart_refueling_curve_progress_Site_ItemKey]
        ON [dbo].[pano_smart_refueling_curve_progress] ([Site], [ItemKey]);
END;
