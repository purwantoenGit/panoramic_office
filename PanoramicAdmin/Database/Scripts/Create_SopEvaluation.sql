USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_sop_evaluasi', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_sop_evaluasi]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ApplicationName] nvarchar(100) NOT NULL,
        [SopCategory] nvarchar(100) NOT NULL,
        [Level] tinyint NULL,
        [DocumentTitle] nvarchar(250) NULL,
        [DocumentNumber] nvarchar(100) NULL,
        [Revision] nvarchar(50) NULL,
        [OwnerName] nvarchar(150) NULL,
        [EvidenceUrl] nvarchar(1000) NULL,
        [EvaluationDate] date NULL,
        [Findings] nvarchar(max) NULL,
        [FollowUpAction] nvarchar(max) NULL,
        [FollowUpDueDate] date NULL,
        [UpdatedAt] datetime2(0) NOT NULL
            CONSTRAINT [DF_pano_sop_evaluasi_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_pano_sop_evaluasi] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_pano_sop_evaluasi_Level]
            CHECK ([Level] IS NULL OR [Level] BETWEEN 0 AND 4),
        CONSTRAINT [UX_pano_sop_evaluasi_Application_Category]
            UNIQUE ([ApplicationName], [SopCategory])
    );
END;

INSERT INTO [dbo].[pano_sop_evaluasi] ([ApplicationName], [SopCategory])
SELECT applications.[ApplicationName], categories.[SopCategory]
FROM (VALUES
    (N'OPA'),
    (N'GPS TRACKING'),
    (N'IFLASH'),
    (N'OMP'),
    (N'CAS'),
    (N'OBSMS'),
    (N'MH02'),
    (N'H2D'),
    (N'MIR'),
    (N'REMOTE DOZER')
) AS applications ([ApplicationName])
CROSS JOIN (VALUES
    (N'Operational Usage'),
    (N'Monitoring & Follow-up'),
    (N'Exception & Escalation'),
    (N'Technical Support & Recovery'),
    (N'Performance & Compliance Evaluation')
) AS categories ([SopCategory])
WHERE NOT EXISTS
(
    SELECT 1
    FROM [dbo].[pano_sop_evaluasi] AS existing
    WHERE existing.[ApplicationName] = applications.[ApplicationName]
      AND existing.[SopCategory] = categories.[SopCategory]
);
