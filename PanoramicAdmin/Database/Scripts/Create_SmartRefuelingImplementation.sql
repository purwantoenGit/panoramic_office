USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_smart_refueling_implementation', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_smart_refueling_implementation]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Site] nvarchar(150) NOT NULL,
        [Activity] nvarchar(250) NOT NULL,
        [Status2026Q1] nvarchar(20) NULL,
        [Status2026Q2] nvarchar(20) NULL,
        [Status2026Q3] nvarchar(20) NULL,
        [Status2026Q4] nvarchar(20) NULL,
        [Status2027Q1] nvarchar(20) NULL,
        [Status2027Q2] nvarchar(20) NULL,
        [Status2027Q3] nvarchar(20) NULL,
        [Status2027Q4] nvarchar(20) NULL,
        CONSTRAINT [PK_pano_smart_refueling_implementation] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_pano_smart_refueling_implementation_Statuses]
            CHECK
            (
                ([Status2026Q1] IS NULL OR [Status2026Q1] IN (N'Done', N'Progress', N'Overdue', N'Plan', N'Unplanned'))
                AND ([Status2026Q2] IS NULL OR [Status2026Q2] IN (N'Done', N'Progress', N'Overdue', N'Plan', N'Unplanned'))
                AND ([Status2026Q3] IS NULL OR [Status2026Q3] IN (N'Done', N'Progress', N'Overdue', N'Plan', N'Unplanned'))
                AND ([Status2026Q4] IS NULL OR [Status2026Q4] IN (N'Done', N'Progress', N'Overdue', N'Plan', N'Unplanned'))
                AND ([Status2027Q1] IS NULL OR [Status2027Q1] IN (N'Done', N'Progress', N'Overdue', N'Plan', N'Unplanned'))
                AND ([Status2027Q2] IS NULL OR [Status2027Q2] IN (N'Done', N'Progress', N'Overdue', N'Plan', N'Unplanned'))
                AND ([Status2027Q3] IS NULL OR [Status2027Q3] IN (N'Done', N'Progress', N'Overdue', N'Plan', N'Unplanned'))
                AND ([Status2027Q4] IS NULL OR [Status2027Q4] IN (N'Done', N'Progress', N'Overdue', N'Plan', N'Unplanned'))
            )
    );

    CREATE INDEX [IX_pano_smart_refueling_implementation_Site_Activity]
        ON [dbo].[pano_smart_refueling_implementation] ([Site], [Activity]);
END;
