USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_project_issue', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_project_issue]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ProjectId] int NOT NULL,
        [StatusProject] nvarchar(200) NOT NULL,
        [Tanggal] date NOT NULL,
        [IssuerPicId] int NOT NULL,
        [StrategicActionId] int NOT NULL,
        [Deskripsi] nvarchar(max) NULL,
        [Feedback] nvarchar(max) NULL,
        [PicId] int NULL,
        [DueDate] date NULL,
        CONSTRAINT [PK_pano_project_issue] PRIMARY KEY ([Id])
    );
END;

IF OBJECT_ID(N'dbo.pano_project_issue', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.pano_project_issue', N'U')
         AND name = N'StatusProject'
         AND max_length > 0
         AND max_length < 400
   )
BEGIN
    ALTER TABLE [dbo].[pano_project_issue]
        ALTER COLUMN [StatusProject] nvarchar(200) NOT NULL;
END;

DECLARE @IssueTableId int = OBJECT_ID(N'dbo.pano_project_issue', N'U');
DECLARE @OldActivityPlanColumnId int;
DECLARE @StrategicActionColumnId int;

IF @IssueTableId IS NOT NULL
BEGIN
    SELECT @OldActivityPlanColumnId = column_id
    FROM sys.columns
    WHERE object_id = @IssueTableId AND name = N'MasterActivityPlanId';

    SELECT @StrategicActionColumnId = column_id
    FROM sys.columns
    WHERE object_id = @IssueTableId AND name = N'StrategicActionId';

    IF @OldActivityPlanColumnId IS NOT NULL AND @StrategicActionColumnId IS NULL
    BEGIN
        DECLARE @LegacyForeignKey sysname;
        DECLARE @DropLegacyForeignKeySql nvarchar(max);
        DECLARE legacy_foreign_keys CURSOR LOCAL FAST_FORWARD FOR
            SELECT DISTINCT fk.name
            FROM sys.foreign_keys AS fk
            INNER JOIN sys.foreign_key_columns AS fkc
                ON fkc.constraint_object_id = fk.object_id
            WHERE fkc.parent_object_id = @IssueTableId
              AND fkc.parent_column_id = @OldActivityPlanColumnId;

        OPEN legacy_foreign_keys;
        FETCH NEXT FROM legacy_foreign_keys INTO @LegacyForeignKey;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @DropLegacyForeignKeySql =
                N'ALTER TABLE [dbo].[pano_project_issue] DROP CONSTRAINT '
                + QUOTENAME(@LegacyForeignKey) + N';';
            EXEC sys.sp_executesql @DropLegacyForeignKeySql;
            FETCH NEXT FROM legacy_foreign_keys INTO @LegacyForeignKey;
        END;
        CLOSE legacy_foreign_keys;
        DEALLOCATE legacy_foreign_keys;

        EXEC sys.sp_rename
            N'dbo.pano_project_issue.MasterActivityPlanId',
            N'StrategicActionId',
            N'COLUMN';
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = @IssueTableId AND name = N'StrategicActionId'
    )
    BEGIN
        ALTER TABLE [dbo].[pano_project_issue]
            ADD [StrategicActionId] int NULL;
    END;
END;

IF OBJECT_ID(N'dbo.pano_project_issue', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.pano_strategic_action', N'U') IS NOT NULL
BEGIN
    DECLARE @EnsureStrategicActionSql nvarchar(max) = N'
        IF EXISTS
        (
            SELECT 1
            FROM [dbo].[pano_project_issue] AS issue
            LEFT JOIN [dbo].[pano_strategic_action] AS [action]
                ON [action].[Id] = issue.[StrategicActionId]
            WHERE issue.[StrategicActionId] IS NOT NULL
              AND [action].[Id] IS NULL
        )
        BEGIN
            SET IDENTITY_INSERT [dbo].[pano_strategic_action] ON;

            INSERT INTO [dbo].[pano_strategic_action] ([Id], [NamaStrategicAction])
            SELECT DISTINCT issue.[StrategicActionId],
                   N''Strategic Action '' + CONVERT(nvarchar(20), issue.[StrategicActionId])
            FROM [dbo].[pano_project_issue] AS issue
            WHERE issue.[StrategicActionId] IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [dbo].[pano_strategic_action] AS [action]
                  WHERE [action].[Id] = issue.[StrategicActionId]
              );

            SET IDENTITY_INSERT [dbo].[pano_strategic_action] OFF;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM [dbo].[pano_project_issue]
            WHERE [StrategicActionId] IS NULL
        )
        BEGIN
            INSERT INTO [dbo].[pano_strategic_action] ([NamaStrategicAction])
            VALUES (N''Unassigned Strategic Action'');

            DECLARE @DefaultStrategicActionId int = CONVERT(int, SCOPE_IDENTITY());
            UPDATE [dbo].[pano_project_issue]
            SET [StrategicActionId] = @DefaultStrategicActionId
            WHERE [StrategicActionId] IS NULL;
        END;

        ALTER TABLE [dbo].[pano_project_issue]
            ALTER COLUMN [StrategicActionId] int NOT NULL;';

    EXEC sys.sp_executesql @EnsureStrategicActionSql;
END;

IF OBJECT_ID(N'dbo.pano_project_issue', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.foreign_keys
       WHERE name = N'FK_pano_project_issue_pano_MasterProject_ProjectId'
         AND parent_object_id = OBJECT_ID(N'dbo.pano_project_issue', N'U')
   )
BEGIN
    ALTER TABLE [dbo].[pano_project_issue]
        ADD CONSTRAINT [FK_pano_project_issue_pano_MasterProject_ProjectId]
        FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[pano_MasterProject] ([Id]) ON DELETE NO ACTION;
END;

IF OBJECT_ID(N'dbo.pano_project_issue', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.foreign_keys
       WHERE name = N'FK_pano_project_issue_pano_master_pic_IssuerPicId'
         AND parent_object_id = OBJECT_ID(N'dbo.pano_project_issue', N'U')
   )
BEGIN
    ALTER TABLE [dbo].[pano_project_issue]
        ADD CONSTRAINT [FK_pano_project_issue_pano_master_pic_IssuerPicId]
        FOREIGN KEY ([IssuerPicId]) REFERENCES [dbo].[pano_master_pic] ([Id]) ON DELETE NO ACTION;
END;

IF OBJECT_ID(N'dbo.pano_project_issue', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.foreign_keys
       WHERE name = N'FK_pano_project_issue_pano_strategic_action_StrategicActionId'
         AND parent_object_id = OBJECT_ID(N'dbo.pano_project_issue', N'U')
   )
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE [dbo].[pano_project_issue]
            ADD CONSTRAINT [FK_pano_project_issue_pano_strategic_action_StrategicActionId]
            FOREIGN KEY ([StrategicActionId]) REFERENCES [dbo].[pano_strategic_action] ([Id]) ON DELETE NO ACTION;';
END;

IF OBJECT_ID(N'dbo.pano_project_issue', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.foreign_keys
       WHERE name = N'FK_pano_project_issue_pano_master_pic_PicId'
         AND parent_object_id = OBJECT_ID(N'dbo.pano_project_issue', N'U')
   )
BEGIN
    ALTER TABLE [dbo].[pano_project_issue]
        ADD CONSTRAINT [FK_pano_project_issue_pano_master_pic_PicId]
        FOREIGN KEY ([PicId]) REFERENCES [dbo].[pano_master_pic] ([Id]) ON DELETE NO ACTION;
END;

IF OBJECT_ID(N'dbo.pano_project_issue_action_plan', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_project_issue_action_plan]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ProjectIssueId] int NOT NULL,
        [ActionPlan] nvarchar(max) NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        [PicId] int NOT NULL,
        [Feedback] nvarchar(max) NULL,
        CONSTRAINT [PK_pano_project_issue_action_plan] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_pano_project_issue_action_plan_ProjectIssueId]
            FOREIGN KEY ([ProjectIssueId])
            REFERENCES [dbo].[pano_project_issue] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_pano_project_issue_action_plan_PicId]
            FOREIGN KEY ([PicId])
            REFERENCES [dbo].[pano_master_pic] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_pano_project_issue_action_plan_ProjectIssueId]
        ON [dbo].[pano_project_issue_action_plan] ([ProjectIssueId]);
END;

IF OBJECT_ID(N'dbo.pano_project_issue_action_plan', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.pano_project_issue_action_plan', N'Status') IS NULL
BEGIN
    ALTER TABLE [dbo].[pano_project_issue_action_plan]
        ADD [Status] nvarchar(200) NULL;
END;

IF OBJECT_ID(N'dbo.pano_project_issue_action_plan', N'U') IS NOT NULL
BEGIN
    DECLARE @EnsureActionPlanStatusSql nvarchar(max) = N'
        UPDATE actionPlan
        SET [Status] = issue.[StatusProject]
        FROM [dbo].[pano_project_issue_action_plan] AS actionPlan
        INNER JOIN [dbo].[pano_project_issue] AS issue
            ON issue.[Id] = actionPlan.[ProjectIssueId]
        WHERE actionPlan.[Status] IS NULL;

        IF EXISTS
        (
            SELECT 1
            FROM [dbo].[pano_project_issue_action_plan]
            WHERE [Status] IS NULL
        )
        BEGIN
            THROW 50001, N''Unable to migrate Action Plan status from Project Issue.'', 1;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N''dbo.pano_project_issue_action_plan'', N''U'')
              AND name = N''Status''
              AND max_length > 0
              AND max_length < 400
        )
           OR EXISTS
           (
               SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N''dbo.pano_project_issue_action_plan'', N''U'')
                 AND name = N''Status''
                 AND is_nullable = 1
           )
        BEGIN
            ALTER TABLE [dbo].[pano_project_issue_action_plan]
                ALTER COLUMN [Status] nvarchar(200) NOT NULL;
        END;';

    EXEC sys.sp_executesql @EnsureActionPlanStatusSql;
END;

IF OBJECT_ID(N'dbo.pano_project_issue_action_plan', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.pano_project_issue', N'Deskripsi') IS NOT NULL
   AND COL_LENGTH(N'dbo.pano_project_issue', N'PicId') IS NOT NULL
BEGIN
    DECLARE @MigrateLegacyActionPlansSql nvarchar(max) = N'
        INSERT INTO [dbo].[pano_project_issue_action_plan]
            ([ProjectIssueId], [ActionPlan], [Status], [PicId], [Feedback])
        SELECT issue.[Id], issue.[Deskripsi], issue.[StatusProject], issue.[PicId], issue.[Feedback]
        FROM [dbo].[pano_project_issue] AS issue
        WHERE issue.[PicId] IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM [dbo].[pano_project_issue_action_plan] AS actionPlan
              WHERE actionPlan.[ProjectIssueId] = issue.[Id]
          );';

    EXEC sys.sp_executesql @MigrateLegacyActionPlansSql;

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.pano_project_issue', N'U')
          AND name = N'Deskripsi'
          AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE [dbo].[pano_project_issue]
            ALTER COLUMN [Deskripsi] nvarchar(max) NULL;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.pano_project_issue', N'U')
          AND name = N'PicId'
          AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE [dbo].[pano_project_issue]
            ALTER COLUMN [PicId] int NULL;
    END;
END;
