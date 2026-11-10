using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class ScrumProjectViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<ScrumProjectViewModel> logger)
{
    public static IReadOnlyList<string> SprintStatuses { get; } =
        ["Direncanakan", "Berjalan", "Selesai"];
    public static IReadOnlyList<string> TaskStatuses { get; } =
        ["Belum dimulai", "Sedang dikerjakan", "Dalam review", "Selesai"];
    public static IReadOnlyList<string> Priorities { get; } =
        ["Rendah", "Sedang", "Tinggi"];

    public event Action? StateChanged;
    public IReadOnlyList<ScrumSprintRow> Sprints { get; private set; } = [];
    public IReadOnlyList<ScrumTaskRow> Tasks { get; private set; } = [];
    public IReadOnlyList<ScrumBlockerRow> Blockers { get; private set; } = [];
    public ScrumSprintRow? SelectedSprint =>
        Sprints.FirstOrDefault(sprint => sprint.Id == SelectedSprintId);
    public int? SelectedSprintId { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsSaving { get; private set; }
    public string? DatabaseError { get; private set; }
    public int DaysRemaining =>
        SelectedSprint is { } sprint
            ? Math.Max(0, (sprint.EndDate.Date - DateTime.Today).Days)
            : 0;
    public int CompletedTaskCount =>
        Tasks.Count(task => task.Status == "Selesai");
    public int InProgressTaskCount =>
        Tasks.Count(task => task.Status == "Sedang dikerjakan");
    public int ActiveBlockerCount =>
        Blockers.Count(blocker => !blocker.IsResolved);
    public double CompletionPercent =>
        Tasks.Count == 0 ? 0 : (double)CompletedTaskCount / Tasks.Count * 100;

    public async Task LoadAsync(int? preferredSprintId = null)
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
            Sprints = (await db.ScrumSprints.AsNoTracking()
                    .OrderByDescending(sprint => sprint.Status == "Berjalan")
                    .ThenByDescending(sprint => sprint.StartDate)
                    .Select(sprint => new ScrumSprintRow(
                        sprint.Id,
                        sprint.Name,
                        sprint.Focus,
                        sprint.StartDate,
                        sprint.EndDate,
                        sprint.Status))
                    .ToListAsync());
            SelectedSprintId = SelectSprintId(preferredSprintId);

            if (SelectedSprintId is not { } sprintId)
            {
                Tasks = [];
                Blockers = [];
                return;
            }

            Tasks = (await db.ScrumTasks.AsNoTracking()
                    .Where(task => task.SprintId == sprintId)
                    .OrderBy(task => task.Id)
                    .Select(task => new ScrumTaskRow(
                        task.Id,
                        $"ACN-{task.Id:000}",
                        task.SprintId,
                        task.Title,
                        task.Owner ?? string.Empty,
                        GetInitials(task.Owner),
                        task.Priority,
                        task.Status,
                        task.DueDate))
                    .ToListAsync());
            Blockers = (await db.ScrumBlockers.AsNoTracking()
                    .Where(blocker => blocker.SprintId == sprintId)
                    .OrderBy(blocker => blocker.IsResolved)
                    .ThenBy(blocker => blocker.Id)
                    .Select(blocker => new ScrumBlockerRow(
                        blocker.Id,
                        blocker.SprintId,
                        blocker.Title,
                        blocker.Detail,
                        blocker.Owner ?? string.Empty,
                        blocker.NextStep,
                        blocker.IsResolved))
                    .ToListAsync());
        }
        catch (Exception exception)
        {
            HandleError(exception, "memuat Scrum Monitoring",
                "Data Scrum Monitoring gagal dimuat.");
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public Task SelectSprintAsync(int sprintId) => LoadAsync(sprintId);

    public async Task SaveSprintAsync(ScrumSprintInput input)
    {
        var name = input.Name.Trim();
        var focus = input.Focus.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 ||
            string.IsNullOrWhiteSpace(focus) || focus.Length > 250 ||
            input.EndDate.Date < input.StartDate.Date ||
            !SprintStatuses.Contains(input.Status, StringComparer.Ordinal))
        {
            snackbar.Add("Periksa nama, fokus, rentang tanggal, dan status sprint.", Severity.Warning);
            return;
        }

        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            ScrumSprint entity;
            if (input.Id is { } id)
            {
                entity = await db.ScrumSprints.SingleOrDefaultAsync(sprint => sprint.Id == id)
                    ?? throw new InvalidOperationException("Sprint tidak ditemukan.");
            }
            else
            {
                entity = new ScrumSprint
                {
                    Name = name,
                    Focus = focus,
                    StartDate = input.StartDate.Date,
                    EndDate = input.EndDate.Date,
                    Status = input.Status
                };
                db.ScrumSprints.Add(entity);
            }

            entity.Name = name;
            entity.Focus = focus;
            entity.StartDate = input.StartDate.Date;
            entity.EndDate = input.EndDate.Date;
            entity.Status = input.Status;
            await db.SaveChangesAsync();
            snackbar.Add("Sprint berhasil disimpan.", Severity.Success);
            await LoadAsync(entity.Id);
        }
        catch (Exception exception)
        {
            HandleError(exception, "menyimpan sprint", "Sprint gagal disimpan.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task DeleteSprintAsync(int id)
    {
        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var sprint = await db.ScrumSprints.SingleOrDefaultAsync(item => item.Id == id);
            if (sprint is null)
            {
                snackbar.Add("Sprint tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            if (await db.ScrumTasks.AnyAsync(task => task.SprintId == id) ||
                await db.ScrumBlockers.AnyAsync(blocker => blocker.SprintId == id))
            {
                snackbar.Add("Sprint tidak bisa dihapus selama masih memiliki task atau blocker.", Severity.Warning);
                return;
            }

            db.ScrumSprints.Remove(sprint);
            await db.SaveChangesAsync();
            snackbar.Add("Sprint berhasil dihapus.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, "menghapus sprint", "Sprint gagal dihapus.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task SaveTaskAsync(ScrumTaskInput input)
    {
        var title = input.Title.Trim();
        var owner = Clean(input.Owner);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 250 ||
            owner?.Length > 150 ||
            !TaskStatuses.Contains(input.Status, StringComparer.Ordinal) ||
            !Priorities.Contains(input.Priority, StringComparer.Ordinal) ||
            !Sprints.Any(sprint => sprint.Id == input.SprintId))
        {
            snackbar.Add("Periksa judul, PIC, prioritas, status, dan sprint task.", Severity.Warning);
            return;
        }

        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            ScrumTask entity;
            if (input.Id is { } id)
            {
                entity = await db.ScrumTasks.SingleOrDefaultAsync(task => task.Id == id)
                    ?? throw new InvalidOperationException("Task tidak ditemukan.");
            }
            else
            {
                entity = new ScrumTask
                {
                    SprintId = input.SprintId,
                    Title = title,
                    Priority = input.Priority,
                    Status = input.Status
                };
                db.ScrumTasks.Add(entity);
            }

            entity.SprintId = input.SprintId;
            entity.Title = title;
            entity.Owner = owner;
            entity.Priority = input.Priority;
            entity.Status = input.Status;
            entity.DueDate = input.DueDate?.Date;
            await db.SaveChangesAsync();
            snackbar.Add("Task berhasil disimpan.", Severity.Success);
            await LoadAsync(input.SprintId);
        }
        catch (Exception exception)
        {
            HandleError(exception, "menyimpan task", "Task gagal disimpan.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task DeleteTaskAsync(int id)
    {
        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var task = await db.ScrumTasks.SingleOrDefaultAsync(item => item.Id == id);
            if (task is null)
            {
                snackbar.Add("Task tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync(SelectedSprintId);
                return;
            }

            var sprintId = task.SprintId;
            db.ScrumTasks.Remove(task);
            await db.SaveChangesAsync();
            snackbar.Add("Task berhasil dihapus.", Severity.Success);
            await LoadAsync(sprintId);
        }
        catch (Exception exception)
        {
            HandleError(exception, "menghapus task", "Task gagal dihapus.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task SaveBlockerAsync(ScrumBlockerInput input)
    {
        var title = input.Title.Trim();
        var detail = input.Detail.Trim();
        var owner = Clean(input.Owner);
        var nextStep = input.NextStep.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 250 ||
            string.IsNullOrWhiteSpace(detail) ||
            owner?.Length > 150 ||
            string.IsNullOrWhiteSpace(nextStep) ||
            !Sprints.Any(sprint => sprint.Id == input.SprintId))
        {
            snackbar.Add("Periksa judul, detail, PIC, dan tindak lanjut blocker.", Severity.Warning);
            return;
        }

        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            ScrumBlocker entity;
            if (input.Id is { } id)
            {
                entity = await db.ScrumBlockers.SingleOrDefaultAsync(blocker => blocker.Id == id)
                    ?? throw new InvalidOperationException("Blocker tidak ditemukan.");
            }
            else
            {
                entity = new ScrumBlocker
                {
                    SprintId = input.SprintId,
                    Title = title,
                    Detail = detail,
                    NextStep = nextStep
                };
                db.ScrumBlockers.Add(entity);
            }

            entity.SprintId = input.SprintId;
            entity.Title = title;
            entity.Detail = detail;
            entity.Owner = owner;
            entity.NextStep = nextStep;
            entity.IsResolved = input.IsResolved;
            await db.SaveChangesAsync();
            snackbar.Add("Blocker berhasil disimpan.", Severity.Success);
            await LoadAsync(input.SprintId);
        }
        catch (Exception exception)
        {
            HandleError(exception, "menyimpan blocker", "Blocker gagal disimpan.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task DeleteBlockerAsync(int id)
    {
        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var blocker = await db.ScrumBlockers.SingleOrDefaultAsync(item => item.Id == id);
            if (blocker is null)
            {
                snackbar.Add("Blocker tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync(SelectedSprintId);
                return;
            }

            var sprintId = blocker.SprintId;
            db.ScrumBlockers.Remove(blocker);
            await db.SaveChangesAsync();
            snackbar.Add("Blocker berhasil dihapus.", Severity.Success);
            await LoadAsync(sprintId);
        }
        catch (Exception exception)
        {
            HandleError(exception, "menghapus blocker", "Blocker gagal dihapus.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private int? SelectSprintId(int? preferredSprintId)
    {
        if (preferredSprintId is { } requestedId &&
            Sprints.Any(sprint => sprint.Id == requestedId))
        {
            return requestedId;
        }

        return Sprints.FirstOrDefault(sprint => sprint.Status == "Berjalan")?.Id
            ?? Sprints.FirstOrDefault()?.Id;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetInitials(string? owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return "—";
        }

        return string.Join(string.Empty, owner.Split(' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0])));
    }

    private void HandleError(Exception exception, string operation, string fallback)
    {
        if (exception.GetBaseException() is SqlException)
        {
            logger.LogError(exception, "SQL Server error while {Operation}.", operation);
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
        }
        else
        {
            logger.LogError(exception, "Failed while {Operation}.", operation);
            DatabaseError = fallback;
        }

        snackbar.Add(DatabaseError, Severity.Error);
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
