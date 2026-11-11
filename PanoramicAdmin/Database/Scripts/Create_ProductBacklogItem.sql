USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_scrum_product_backlog_item', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_scrum_product_backlog_item]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Initiative] nvarchar(20) NOT NULL
            CONSTRAINT [DF_pano_scrum_product_backlog_item_Initiative] DEFAULT (N'SMARE'),
        [InitiativeNumber] int NOT NULL,
        [Module] nvarchar(100) NOT NULL,
        [Summary] nvarchar(250) NOT NULL,
        [AcceptanceCriteria] nvarchar(max) NOT NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [TargetSprint] int NULL,
        [DefinitionOfDone] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_pano_scrum_product_backlog_item] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_pano_scrum_product_backlog_item_Initiative]
            CHECK ([Initiative] IN (N'SMARE', N'OMP')),
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
    IF COL_LENGTH(N'dbo.pano_scrum_product_backlog_item', N'Initiative') IS NULL
    BEGIN
        ALTER TABLE [dbo].[pano_scrum_product_backlog_item]
            ADD [Initiative] nvarchar(20) NOT NULL
                CONSTRAINT [DF_pano_scrum_product_backlog_item_Initiative] DEFAULT (N'SMARE');
    END;

    IF COL_LENGTH(N'dbo.pano_scrum_product_backlog_item', N'InitiativeNumber') IS NULL
    BEGIN
        ALTER TABLE [dbo].[pano_scrum_product_backlog_item]
            ADD [InitiativeNumber] int NULL;
    END;

    EXEC(N'UPDATE [dbo].[pano_scrum_product_backlog_item]
        SET [InitiativeNumber] = [Id]
        WHERE [InitiativeNumber] IS NULL;
        IF EXISTS
        (
            SELECT 1
            FROM sys.columns
            WHERE [object_id] = OBJECT_ID(N''dbo.pano_scrum_product_backlog_item'')
              AND [name] = N''InitiativeNumber''
              AND [is_nullable] = 1
        )
        BEGIN
            ALTER TABLE [dbo].[pano_scrum_product_backlog_item]
                ALTER COLUMN [InitiativeNumber] int NOT NULL;
        END;');

    IF OBJECT_ID(N'dbo.CK_pano_scrum_product_backlog_item_Initiative', N'C') IS NULL
    BEGIN
        EXEC(N'ALTER TABLE [dbo].[pano_scrum_product_backlog_item]
            ADD CONSTRAINT [CK_pano_scrum_product_backlog_item_Initiative]
                CHECK ([Initiative] IN (N''SMARE'', N''OMP''));');
    END;

    IF OBJECT_ID(N'dbo.FK_pano_scrum_product_backlog_item_TargetSprint', N'F') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[pano_scrum_product_backlog_item]
            DROP CONSTRAINT [FK_pano_scrum_product_backlog_item_TargetSprint];
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE [object_id] = OBJECT_ID(N'dbo.pano_scrum_product_backlog_item')
          AND [name] = N'UX_pano_scrum_product_backlog_item_Initiative_Number'
    )
    BEGIN
        CREATE UNIQUE INDEX [UX_pano_scrum_product_backlog_item_Initiative_Number]
            ON [dbo].[pano_scrum_product_backlog_item] ([Initiative], [InitiativeNumber]);
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

IF OBJECT_ID(N'dbo.pano_scrum_product_backlog_item_image', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_scrum_product_backlog_item_image]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ProductBacklogItemId] int NOT NULL,
        [FileName] nvarchar(100) NOT NULL,
        [ContentType] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_pano_scrum_product_backlog_item_image] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_pano_scrum_product_backlog_item_image_PBI]
            FOREIGN KEY ([ProductBacklogItemId])
            REFERENCES [dbo].[pano_scrum_product_backlog_item] ([Id])
            ON DELETE CASCADE
    );

    CREATE INDEX [IX_pano_scrum_product_backlog_item_image_PBI]
        ON [dbo].[pano_scrum_product_backlog_item_image] ([ProductBacklogItemId]);
END;
