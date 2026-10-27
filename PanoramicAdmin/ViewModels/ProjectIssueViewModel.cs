using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class ProjectIssueViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<ProjectIssueViewModel> logger)
{
    public event Action? StateChanged;
    public IReadOnlyList<ProjectIssueRow> Issues { get; private set; } = [];
    public IReadOnlyList<ProjectIssueRow> FilteredIssues { get; private set; } = [];
    public IReadOnlyList<ReferenceOption> Projects { get; private set; } = [];
    public IReadOnlyList<ReferenceOption> Pics { get; private set; } = [];
    public IReadOnlyList<ReferenceOption> StrategicActions { get; private set; } = [];
    public IReadOnlyList<string> ProjectStatuses { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public string? DatabaseError { get; private set; }
    public int? PendingDeleteId { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsSaving { get; private set; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        DatabaseError = null;
        NotifyStateChanged();
        try
        {
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("PanoramicDb")))
            {
                DatabaseError = "Konfigurasi koneksi database PanoramicDb belum tersedia.";
                logger.LogError("The PanoramicDb connection string is missing or empty.");
                return;
            }

            await using var db = await dbContextFactory.CreateDbContextAsync();
            Projects = await db.Projects.AsNoTracking()
                .OrderBy(project => project.Name)
                .Select(project => new ReferenceOption(project.Id, project.Name))
                .ToListAsync();
            Pics = await db.MasterPics.AsNoTracking()
                .OrderBy(pic => pic.Nama)
                .Select(pic => new ReferenceOption(pic.Id, pic.Nama))
                .ToListAsync();
            StrategicActions = await db.StrategicActions.AsNoTracking()
                .OrderBy(action => action.NamaStrategicAction)
                .Select(action => new ReferenceOption(action.Id, action.NamaStrategicAction))
                .ToListAsync();
            ProjectStatuses = await db.MasterIssues.AsNoTracking()
                .Where(issue => issue.Active)
                .OrderBy(issue => issue.Issue)
                .Select(issue => issue.Issue)
                .ToListAsync();

            var issues = await db.ProjectIssues.AsNoTracking()
                .Include(issue => issue.Project)
                .Include(issue => issue.IssuerPic)
                .Include(issue => issue.StrategicAction)
                .Include(issue => issue.ActionPlans)
                    .ThenInclude(plan => plan.Pic)
                .Include(issue => issue.ActionPlans)
                    .ThenInclude(plan => plan.PicAssignments)
                        .ThenInclude(assignment => assignment.Pic)
                .OrderBy(issue => issue.Id)
                .ToListAsync();
            Issues = issues.Select((issue, index) => new ProjectIssueRow(
                issue.Id,
                index + 1,
                issue.ProjectId,
                issue.Project?.Name ?? "(Project tidak tersedia)",
                issue.StatusProject,
                issue.Tanggal,
                issue.IssuerPicId,
                issue.IssuerPic?.Nama ?? "(PIC tidak tersedia)",
                issue.StrategicActionId,
                issue.StrategicAction?.NamaStrategicAction ?? "(Strategic Action tidak tersedia)",
                issue.ActionPlans.Select(plan =>
                {
                    var assignedPics = plan.PicAssignments
                        .Select(assignment => assignment.Pic)
                        .OfType<MasterPic>()
                        .Select(pic => new ReferenceOption(pic.Id, pic.Nama))
                        .ToList();
                    if (assignedPics.Count == 0 && plan.Pic is not null)
                    {
                        assignedPics.Add(new ReferenceOption(plan.Pic.Id, plan.Pic.Nama));
                    }

                    return new ProjectIssueActionPlanRow(
                        plan.Id,
                        plan.ActionPlan,
                        plan.Status,
                        assignedPics.Select(pic => pic.Id).ToArray(),
                        assignedPics,
                        plan.Feedback);
                }).ToList(),
                issue.DueDate)).ToList();
            ApplyFilter();
        }
        catch (Exception exception)
        {
            HandleError(exception, "memuat Project Issue", "Data Project Issue gagal dimuat.");
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public void Search(string? text)
    {
        SearchText = text ?? string.Empty;
        ApplyFilter();
        NotifyStateChanged();
    }

    private void ApplyFilter()
    {
        FilteredIssues = Issues
            .Where(issue =>
                issue.Project.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                issue.StatusProject.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                issue.Issuer.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                issue.StrategicAction.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                issue.ActionPlans.Any(plan =>
                    plan.ActionPlan.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    plan.Status.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    plan.Pics.Any(pic => pic.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (plan.Feedback?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)) ||
                issue.Tanggal.ToString("dd/MM/yyyy").Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (issue.DueDate?.ToString("dd/MM/yyyy").Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select((issue, index) => issue with { No = index + 1 })
            .ToList();
    }

    public async Task CreateAsync(ProjectIssueInput input) => await SaveAsync(null, input);
    public async Task UpdateAsync(int id, ProjectIssueInput input) => await SaveAsync(id, input);

    private async Task SaveAsync(int? id, ProjectIssueInput input)
    {
        var status = input.StatusProject.Trim();
        var actionPlans = input.ActionPlans.Select(plan => new ProjectIssueActionPlanInput(
            plan.Id,
            plan.ActionPlan.Trim(),
            plan.Status.Trim(),
            plan.PicIds.Distinct().ToArray(),
            string.IsNullOrWhiteSpace(plan.Feedback) ? null : plan.Feedback.Trim())).ToList();
        if (!Validate(input, status, actionPlans)) return;

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var projectExists = await db.Projects.AnyAsync(project => project.Id == input.ProjectId);
            var issuerExists = await db.MasterPics.AnyAsync(pic => pic.Id == input.IssuerPicId);
            var strategicActionExists = await db.StrategicActions.AnyAsync(action => action.Id == input.StrategicActionId);
            var actionPlanPicIds = actionPlans.SelectMany(plan => plan.PicIds).Distinct().ToArray();
            var validActionPlanPicCount = await db.MasterPics.CountAsync(pic => actionPlanPicIds.Contains(pic.Id));
            if (!projectExists || !issuerExists || !strategicActionExists ||
                validActionPlanPicCount != actionPlanPicIds.Length)
            {
                snackbar.Add("Pilihan Project, Issuer, PIC Action Plan, atau Strategic Action sudah berubah. Muat ulang data dan pilih kembali.", Severity.Warning);
                await LoadAsync();
                return;
            }

            var activeStatuses = await db.MasterIssues.AsNoTracking()
                .Where(masterIssue => masterIssue.Active)
                .Select(masterIssue => masterIssue.Issue)
                .ToListAsync();
            var activeStatusSet = activeStatuses.ToHashSet(StringComparer.Ordinal);
            var statusIsActive = activeStatusSet.Contains(status);
            var isUnchangedStatus = !statusIsActive && id is not null &&
                await db.ProjectIssues.AnyAsync(existingIssue =>
                    existingIssue.Id == id && existingIssue.StatusProject == status);
            if (!statusIsActive && !isUnchangedStatus)
            {
                snackbar.Add("Status Project sudah tidak aktif. Muat ulang data dan pilih Status Project yang aktif.", Severity.Warning);
                await LoadAsync();
                return;
            }

            ProjectIssue issue;
            if (id is null)
            {
                issue = new ProjectIssue { StatusProject = status };
                db.ProjectIssues.Add(issue);
            }
            else
            {
                issue = await db.ProjectIssues
                    .Include(item => item.ActionPlans)
                        .ThenInclude(plan => plan.PicAssignments)
                    .SingleOrDefaultAsync(item => item.Id == id)
                    ?? throw new InvalidOperationException("Project Issue tidak ditemukan.");
            }

            var existingActionPlans = issue.ActionPlans.ToDictionary(plan => plan.Id);
            var submittedIds = actionPlans
                .Where(plan => plan.Id.HasValue)
                .Select(plan => plan.Id!.Value)
                .ToArray();
            if (submittedIds.Distinct().Count() != submittedIds.Length ||
                (id is null && submittedIds.Length > 0) ||
                (id is not null && submittedIds.Any(planId => !existingActionPlans.ContainsKey(planId))))
            {
                snackbar.Add("Daftar Action Plan berubah. Muat ulang issue dan coba lagi.", Severity.Warning);
                await LoadAsync();
                return;
            }

            var invalidActionPlanStatus = actionPlans.Any(plan =>
                !activeStatusSet.Contains(plan.Status) &&
                (plan.Id is not int planId ||
                 !existingActionPlans.TryGetValue(planId, out var existingPlan) ||
                 !string.Equals(existingPlan.Status, plan.Status, StringComparison.Ordinal)));
            if (invalidActionPlanStatus)
            {
                snackbar.Add("Status Action Plan tidak aktif. Pilih status aktif dari Master Issue.", Severity.Warning);
                await LoadAsync();
                return;
            }

            issue.ProjectId = input.ProjectId;
            issue.StatusProject = status;
            issue.Tanggal = input.Tanggal.Date;
            issue.IssuerPicId = input.IssuerPicId;
            issue.StrategicActionId = input.StrategicActionId;
            issue.DueDate = input.DueDate?.Date;
            var retainedActionPlanIds = new HashSet<int>();
            foreach (var actionPlan in actionPlans)
            {
                ProjectIssueActionPlan actionPlanEntity;
                if (actionPlan.Id is int actionPlanId)
                {
                    actionPlanEntity = existingActionPlans[actionPlanId];
                    retainedActionPlanIds.Add(actionPlanId);
                }
                else
                {
                    actionPlanEntity = new ProjectIssueActionPlan
                    {
                        ActionPlan = actionPlan.ActionPlan,
                        Status = actionPlan.Status,
                        PicId = actionPlan.PicIds[0],
                        PicAssignments = actionPlan.PicIds.Select(picId => new ProjectIssueActionPlanPic
                        {
                            PicId = picId
                        }).ToList(),
                        Feedback = actionPlan.Feedback
                    };
                    issue.ActionPlans.Add(actionPlanEntity);
                    continue;
                }

                actionPlanEntity.ActionPlan = actionPlan.ActionPlan;
                actionPlanEntity.Status = actionPlan.Status;
                actionPlanEntity.PicId = actionPlan.PicIds[0];
                db.ProjectIssueActionPlanPics.RemoveRange(actionPlanEntity.PicAssignments);
                actionPlanEntity.PicAssignments.Clear();
                foreach (var picId in actionPlan.PicIds)
                {
                    actionPlanEntity.PicAssignments.Add(new ProjectIssueActionPlanPic
                    {
                        PicId = picId
                    });
                }
                actionPlanEntity.Feedback = actionPlan.Feedback;
            }
            foreach (var removedPlan in issue.ActionPlans
                         .Where(plan => plan.Id != 0 && !retainedActionPlanIds.Contains(plan.Id))
                         .ToList())
            {
                issue.ActionPlans.Remove(removedPlan);
                db.ProjectIssueActionPlans.Remove(removedPlan);
            }

            await db.SaveChangesAsync();
            snackbar.Add(id is null ? "Project Issue berhasil ditambahkan." : "Perubahan Project Issue berhasil disimpan.", Severity.Success);
            SearchText = string.Empty;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, id is null ? "menambahkan Project Issue" : "mengubah Project Issue",
                "Project Issue gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    private bool Validate(
        ProjectIssueInput input,
        string status,
        IReadOnlyList<ProjectIssueActionPlanInput> actionPlans)
    {
        if (input.ProjectId <= 0 || input.IssuerPicId <= 0 || input.StrategicActionId <= 0)
        {
            snackbar.Add("Pilih Project, Issuer, dan Strategic Action.", Severity.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(status) || status.Length > 200)
        {
            snackbar.Add("Status Project wajib diisi dan maksimal 200 karakter.", Severity.Warning);
            return false;
        }
        if (actionPlans.Count == 0 ||
            actionPlans.Any(plan =>
                string.IsNullOrWhiteSpace(plan.ActionPlan) ||
                string.IsNullOrWhiteSpace(plan.Status) ||
                plan.PicIds.Count == 0 ||
                plan.PicIds.Any(picId => picId <= 0)))
        {
            snackbar.Add("Tambahkan minimal satu Action Plan dan pilih Status serta minimal satu PIC untuk setiap Action Plan.", Severity.Warning);
            return false;
        }
        if (input.Tanggal == default)
        {
            snackbar.Add("Tanggal wajib diisi.", Severity.Warning);
            return false;
        }
        return true;
    }

    public void RequestDelete(int id)
    {
        PendingDeleteId = id;
        NotifyStateChanged();
    }

    public void CancelDelete()
    {
        PendingDeleteId = null;
        NotifyStateChanged();
    }

    public async Task DeleteAsync(int id)
    {
        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var issue = await db.ProjectIssues.SingleOrDefaultAsync(item => item.Id == id);
            if (issue is null)
            {
                snackbar.Add("Project Issue tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
            }
            else
            {
                db.ProjectIssues.Remove(issue);
                await db.SaveChangesAsync();
                snackbar.Add("Project Issue berhasil dihapus.", Severity.Success);
            }
            PendingDeleteId = null;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, "menghapus Project Issue", "Project Issue gagal dihapus. Coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    private void HandleError(Exception exception, string operation, string fallback)
    {
        if (exception.GetBaseException() is SqlException)
        {
            logger.LogError(exception, "SQL Server error while {Operation}.", operation);
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server dan pastikan skrip tabel telah dijalankan.";
            snackbar.Add(DatabaseError, Severity.Error);
        }
        else
        {
            logger.LogError(exception, "Failed while {Operation}.", operation);
            DatabaseError = fallback;
            snackbar.Add(fallback, Severity.Error);
        }
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}

public sealed record ReferenceOption(int Id, string Name);

public sealed record ProjectIssueRow(
    int Id,
    int No,
    int ProjectId,
    string Project,
    string StatusProject,
    DateTime Tanggal,
    int IssuerPicId,
    string Issuer,
    int StrategicActionId,
    string StrategicAction,
    IReadOnlyList<ProjectIssueActionPlanRow> ActionPlans,
    DateTime? DueDate);

public sealed record ProjectIssueInput(
    int ProjectId,
    string StatusProject,
    DateTime Tanggal,
    int IssuerPicId,
    int StrategicActionId,
    IReadOnlyList<ProjectIssueActionPlanInput> ActionPlans,
    DateTime? DueDate);

public sealed record ProjectIssueActionPlanRow(
    int Id,
    string ActionPlan,
    string Status,
    IReadOnlyList<int> PicIds,
    IReadOnlyList<ReferenceOption> Pics,
    string? Feedback);

public sealed record ProjectIssueActionPlanInput(
    int? Id,
    string ActionPlan,
    string Status,
    IReadOnlyList<int> PicIds,
    string? Feedback);
