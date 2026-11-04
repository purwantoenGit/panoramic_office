USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_smart_refueling_equipment_actual', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_smart_refueling_equipment_actual]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Site] nvarchar(150) NOT NULL,
        [PitStop] int NULL,
        [FuelTruck] int NULL,
        [TagAntiMetal] int NULL,
        [TagSticker] int NULL,
        [TagSegel] int NULL,
        [KetPitStop] nvarchar(500) NULL,
        [KetFuelTruck] nvarchar(500) NULL,
        [KetTagAntiMetal] nvarchar(500) NULL,
        [KetTagSticker] nvarchar(500) NULL,
        [KetTagSegel] nvarchar(500) NULL,
        CONSTRAINT [PK_pano_smart_refueling_equipment_actual] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_pano_smart_refueling_equipment_actual_NonNegative]
            CHECK
            (
                ([PitStop] IS NULL OR [PitStop] >= 0)
                AND ([FuelTruck] IS NULL OR [FuelTruck] >= 0)
                AND ([TagAntiMetal] IS NULL OR [TagAntiMetal] >= 0)
                AND ([TagSticker] IS NULL OR [TagSticker] >= 0)
                AND ([TagSegel] IS NULL OR [TagSegel] >= 0)
            )
    );

    CREATE UNIQUE INDEX [UX_pano_smart_refueling_equipment_actual_Site]
        ON [dbo].[pano_smart_refueling_equipment_actual] ([Site]);
END;
