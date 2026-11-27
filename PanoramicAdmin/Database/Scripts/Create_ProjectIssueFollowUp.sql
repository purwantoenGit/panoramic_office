USE [db_panoramic];

IF OBJECT_ID(N'dbo.pano_project_issue_action_plan', N'U') IS NULL
BEGIN
    THROW 50010, N'Run Create_ProjectIssue.sql before Create_ProjectIssueFollowUp.sql.', 1;
END;

IF OBJECT_ID(N'dbo.pano_master_pic', N'U') IS NULL
BEGIN
    THROW 50011, N'Run Create_MasterPic.sql before Create_ProjectIssueFollowUp.sql.', 1;
END;

IF OBJECT_ID(N'dbo.pano_project_issue_action_plan_followup_token', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[pano_project_issue_action_plan_followup_token]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [TokenHash] char(64) NOT NULL,
        [ActionPlanId] int NOT NULL,
        [PicId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [UsedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_pano_project_issue_action_plan_followup_token]
            PRIMARY KEY ([Id]),
        CONSTRAINT [FK_pano_project_issue_action_plan_followup_token_ActionPlanId]
            FOREIGN KEY ([ActionPlanId])
            REFERENCES [dbo].[pano_project_issue_action_plan] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_pano_project_issue_action_plan_followup_token_PicId]
            FOREIGN KEY ([PicId])
            REFERENCES [dbo].[pano_master_pic] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.pano_project_issue_action_plan_followup_token', N'U')
      AND [name] = N'UX_pano_project_issue_action_plan_followup_token_TokenHash'
)
BEGIN
    CREATE UNIQUE INDEX [UX_pano_project_issue_action_plan_followup_token_TokenHash]
        ON [dbo].[pano_project_issue_action_plan_followup_token] ([TokenHash]);
END;
