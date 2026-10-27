# Panoramic Admin

Starter admin web app built with .NET 10 Blazor Web App, Interactive Server rendering, and MudBlazor.

## Run locally

From this directory:

```bash
dotnet run
```

Open the URL printed by the .NET CLI. The dashboard is at `/` and the sample user list is at `/users`.

## Live reload in VS Code

From the workspace root, open the Command Palette, choose **Tasks: Run Task**, and select **Watch PanoramicAdmin (Hot Reload)**. Alternatively, from this directory, run:

```bash
dotnet watch run --urls http://localhost:5087
```

Keep the watch process running while editing. Razor and CSS changes are applied automatically in the browser when supported by Hot Reload. Stop any other PanoramicAdmin process using port `5087` first. Changes to startup configuration or dependencies may still require a full restart.

## Structure

- `Components/Pages` contains the dashboard and Razor UI.
- `ViewModels` contains page-specific presentation state and filtering.
- `Services` contains data access abstractions and the sample in-memory implementation.
- `Models` contains application data types.

The dashboard metrics and user list are sample data. Replace `InMemoryUserService` with a persistent implementation and connect the dashboard to real data before production use. Authentication and authorization have not been configured yet.

## Master Project logo setup

Run `Database/Scripts/Create_MasterProject.sql` against `db_panoramic`. The
script adds a `LogoIcon` column to an existing `pano_MasterProject` table and
assigns the default folder icon to existing projects. Project logos can then be
selected from the built-in icon list in the Master Project page.

## Master Category Project database setup

Run `Database/Scripts/Create_MasterCategoryProject.sql` against `db_panoramic`.
If `pano_master_status` exists, the script renames it to
`pano_master_category_project` and renames its `Status` column to
`CategoryProject`, preserving existing rows.

## Master Roles and Master PIC

Run `Database/Scripts/Create_MasterPic.sql`, then
`Database/Scripts/Create_MasterRoles.sql` against `db_panoramic`.
The Master Roles script creates `dbo.pano_master_roles` and imports any existing
non-empty Master PIC role values. Master PIC's Role field is selected from this
list; changing a role updates its assigned PICs, while a role in use cannot be
deleted.

The Virtual Office page displays Master PIC names assigned the
`Project Implementor` role in that room's details.

## Master Issue database setup

Run `Database/Scripts/Create_MasterIssue.sql` against `db_panoramic` to create
`dbo.pano_master_issue` with an `Issue` value and an `Active` checkbox value
that defaults to enabled. Active Master Issue values are the Status Project
choices in the Project Issue add and edit forms.

## Project Issue and Strategic Action database setup

Run `Database/Scripts/Create_MasterIssue.sql` and
`Database/Scripts/Create_StrategicAction.sql` against `db_panoramic` before
`Database/Scripts/Create_ProjectIssue.sql`. The Project Issue script creates the
issue table on a fresh database, or migrates an existing
`MasterActivityPlanId` column in place to `StrategicActionId` without deleting
issue rows, expands `StatusProject` to support the Master Issue field length,
and creates `pano_project_issue_action_plan` for multiple Action Plans per issue
plus `pano_project_issue_action_plan_pic` for assigning multiple PICs to each
Action Plan.
Existing `Deskripsi`, `PicId`, and `Feedback` values are migrated as the first
Action Plan, with the legacy columns retained as nullable fields. Each Action
Plan has its own Status (selected from active Master Issue values), one or more
PICs, and
optional Feedback. Legacy Action Plan status starts with its parent issue's
Status Project. For legacy IDs that have no matching Strategic Action, the
script inserts placeholder names (`Strategic Action {id}`) so the new foreign
key can be created. Existing Strategic Action rows are left unchanged. The
legacy `pano_master_activity_plan` table, if present, is not dropped.
