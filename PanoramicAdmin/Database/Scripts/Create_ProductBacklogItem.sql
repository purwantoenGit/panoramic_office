USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_scrum_product_backlog_item', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_scrum_product_backlog_item]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Module] nvarchar(100) NOT NULL,
        [Summary] nvarchar(250) NOT NULL,
        [AcceptanceCriteria] nvarchar(max) NOT NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [TargetSprint] int NULL,
        [DefinitionOfDone] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_pano_scrum_product_backlog_item] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_pano_scrum_product_backlog_item_Priority]
            CHECK ([Priority] IN (N'Rendah', N'Sedang', N'Tinggi')),
        CONSTRAINT [CK_pano_scrum_product_backlog_item_Status]
            CHECK ([Status] IN (N'Belum dimulai', N'Sedang dikerjakan', N'Dalam review', N'Selesai')),
        CONSTRAINT [CK_pano_scrum_product_backlog_item_TargetSprint]
            CHECK ([TargetSprint] IS NULL OR [TargetSprint] >= 1)
    );
END;

IF OBJECT_ID(N'dbo.pano_scrum_product_backlog_item', N'U') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'dbo.FK_pano_scrum_product_backlog_item_TargetSprint', N'F') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[pano_scrum_product_backlog_item]
            DROP CONSTRAINT [FK_pano_scrum_product_backlog_item_TargetSprint];
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE [object_id] = OBJECT_ID(N'dbo.pano_scrum_product_backlog_item')
          AND [name] = N'IX_pano_scrum_product_backlog_item_TargetSprint'
    )
    BEGIN
        DROP INDEX [IX_pano_scrum_product_backlog_item_TargetSprint]
            ON [dbo].[pano_scrum_product_backlog_item];
    END;

    IF COL_LENGTH(N'dbo.pano_scrum_product_backlog_item', N'TargetSprintId') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[pano_scrum_product_backlog_item]
            DROP COLUMN [TargetSprintId];
    END;

    IF COL_LENGTH(N'dbo.pano_scrum_product_backlog_item', N'TargetSprint') IS NULL
    BEGIN
        ALTER TABLE [dbo].[pano_scrum_product_backlog_item]
            ADD [TargetSprint] int NULL;
    END;

    IF OBJECT_ID(N'dbo.CK_pano_scrum_product_backlog_item_TargetSprint', N'C') IS NULL
    BEGIN
        EXEC(N'ALTER TABLE [dbo].[pano_scrum_product_backlog_item]
            ADD CONSTRAINT [CK_pano_scrum_product_backlog_item_TargetSprint]
                CHECK ([TargetSprint] IS NULL OR [TargetSprint] >= 1);');
    END;
END;
