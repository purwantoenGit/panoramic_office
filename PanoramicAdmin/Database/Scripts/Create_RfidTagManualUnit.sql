USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_add_unit_rfid_tag', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_add_unit_rfid_tag]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [District] nvarchar(200) NOT NULL,
        [EquipClass] nvarchar(10) NOT NULL,
        [Egi] nvarchar(100) NOT NULL,
        [AdditionalUnitCount] int NOT NULL,
        CONSTRAINT [PK_pano_add_unit_rfid_tag] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_pano_add_unit_rfid_tag_AdditionalUnitCount]
            CHECK ([AdditionalUnitCount] >= 0),
        CONSTRAINT [UX_pano_add_unit_rfid_tag_Cell]
            UNIQUE ([District], [EquipClass], [Egi])
    );
END;
