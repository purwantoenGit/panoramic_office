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

IF OBJECT_ID(N'dbo.pano_sop_evaluasi_dokumen', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_sop_evaluasi_dokumen]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [SopEvaluationId] int NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [DocumentNumber] nvarchar(100) NULL,
        [Revision] nvarchar(50) NULL,
        [EvidenceUrl] nvarchar(1000) NULL,
        CONSTRAINT [PK_pano_sop_evaluasi_dokumen] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_pano_sop_evaluasi_dokumen_Evaluasi]
            FOREIGN KEY ([SopEvaluationId]) REFERENCES [dbo].[pano_sop_evaluasi] ([Id])
            ON DELETE CASCADE
    );

    CREATE INDEX [IX_pano_sop_evaluasi_dokumen_EvaluationId]
        ON [dbo].[pano_sop_evaluasi_dokumen] ([SopEvaluationId]);
END;

IF OBJECT_ID(N'dbo.pano_sop_evaluasi_dokumen_anak', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_sop_evaluasi_dokumen_anak]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [SopEvaluationDocumentId] int NOT NULL,
        [DocumentType] nvarchar(3) NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [DocumentNumber] nvarchar(100) NULL,
        [Revision] nvarchar(50) NULL,
        [EvidenceUrl] nvarchar(1000) NULL,
        CONSTRAINT [PK_pano_sop_evaluasi_dokumen_anak] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_pano_sop_evaluasi_dokumen_anak_Type]
            CHECK ([DocumentType] IN (N'INK', N'STD')),
        CONSTRAINT [FK_pano_sop_evaluasi_dokumen_anak_Parent]
            FOREIGN KEY ([SopEvaluationDocumentId])
            REFERENCES [dbo].[pano_sop_evaluasi_dokumen] ([Id])
            ON DELETE CASCADE
    );

    CREATE INDEX [IX_pano_sop_evaluasi_dokumen_anak_ParentDocumentId]
        ON [dbo].[pano_sop_evaluasi_dokumen_anak] ([SopEvaluationDocumentId]);
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

DECLARE @MigratedEvaluations TABLE ([Id] int PRIMARY KEY);

INSERT INTO [dbo].[pano_sop_evaluasi_dokumen]
    ([SopEvaluationId], [Title], [DocumentNumber], [Revision], [EvidenceUrl])
OUTPUT INSERTED.[SopEvaluationId] INTO @MigratedEvaluations ([Id])
SELECT evaluation.[Id],
       COALESCE(
           NULLIF(LTRIM(RTRIM(evaluation.[DocumentTitle])), N''),
           NULLIF(LTRIM(RTRIM(evaluation.[DocumentNumber])), N''),
           N'Dokumen SOP'),
       evaluation.[DocumentNumber],
       evaluation.[Revision],
       evaluation.[EvidenceUrl]
FROM [dbo].[pano_sop_evaluasi] AS evaluation
WHERE
    (evaluation.[DocumentTitle] IS NOT NULL
        OR evaluation.[DocumentNumber] IS NOT NULL
        OR evaluation.[Revision] IS NOT NULL
        OR evaluation.[EvidenceUrl] IS NOT NULL)
    AND NOT EXISTS
    (
        SELECT 1
        FROM [dbo].[pano_sop_evaluasi_dokumen] AS existing
        WHERE existing.[SopEvaluationId] = evaluation.[Id]
    );

UPDATE evaluation
SET [DocumentTitle] = NULL,
    [DocumentNumber] = NULL,
    [Revision] = NULL,
    [EvidenceUrl] = NULL
FROM [dbo].[pano_sop_evaluasi] AS evaluation
INNER JOIN @MigratedEvaluations AS migrated ON migrated.[Id] = evaluation.[Id];
