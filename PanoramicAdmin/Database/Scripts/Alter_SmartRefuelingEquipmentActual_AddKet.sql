USE [db_panoramic];

-- Jalankan jika tabel pano_smart_refueling_equipment_actual sudah dibuat sebelum kolom Ket ada.
IF OBJECT_ID(N'dbo.pano_smart_refueling_equipment_actual', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.pano_smart_refueling_equipment_actual', N'KetPitStop') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_smart_refueling_equipment_actual]
        ADD [KetPitStop] nvarchar(500) NULL,
            [KetFuelTruck] nvarchar(500) NULL,
            [KetTagAntiMetal] nvarchar(500) NULL,
            [KetTagSticker] nvarchar(500) NULL,
            [KetTagSegel] nvarchar(500) NULL;
END;
