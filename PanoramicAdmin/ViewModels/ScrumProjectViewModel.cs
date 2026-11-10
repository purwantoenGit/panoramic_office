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
    public static IReadOnlyList<string> TaskCategories { get; } =
        ["Backend", "Change Management", "Dashboard", "Dokumentasi", "Frontend", "Hardware", "QA", "UR"];
    public static IReadOnlyList<string> Priorities { get; } =
        ["Rendah", "Sedang", "Tinggi"];

    public event Action? StateChanged;
    public IReadOnlyList<ScrumSprintRow> Sprints { get; private set; } = [];
    public IReadOnlyList<ScrumProductBacklogItemOption> ProductBacklogItems { get; private set; } = [];
    public IReadOnlyList<string> PicOptions { get; private set; } = [];
    public IReadOnlyList<ScrumTaskRow> Tasks { get; private set; } = [];
    public ScrumPicFilterOption SelectedPicFilter { get; private set; } = new("Semua PIC", null);
    public IReadOnlyList<ScrumPicFilterOption> PicFilterOptions =>
        new[]
        {
            new ScrumPicFilterOption("Semua PIC", null),
            new ScrumPicFilterOption("Tanpa PIC", string.Empty)
        }
        .Concat(PicOptions.Concat(Tasks.Select(task => task.Owner))
            .Where(pic => !string.IsNullOrWhiteSpace(pic))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pic => pic, StringComparer.OrdinalIgnoreCase)
            .Select(pic => new ScrumPicFilterOption(pic, pic)))
        .ToArray();
    public IReadOnlyList<ScrumTaskRow> FilteredTasks =>
        SelectedPicFilter.Owner switch
        {
            null => Tasks,
            "" => Tasks.Where(task => string.IsNullOrWhiteSpace(task.Owner)).ToArray(),
            var pic => Tasks.Where(task =>
                string.Equals(task.Owner, pic, StringComparison.OrdinalIgnoreCase)).ToArray()
        };
    public IReadOnlyList<ScrumBlockerRow> Blockers { get; private set; } = [];
    public ScrumSprintRow? SelectedSprint =>
        Sprints.FirstOrDefault(sprint => sprint.Id == SelectedSprintId);
    public int? SelectedSprintId { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsSaving { get; private set; }
    public string? DatabaseError { get; private set; }
    public string? SprintDeleteMessage { get; private set; }
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

    public async Task LoadAsync(int? preferredSprintId = null, int? productBacklogItemId = null)
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
            PicOptions = await db.MasterPics.AsNoTracking()
                .OrderBy(pic => pic.Nama)
                .ThenBy(pic => pic.Id)
                .Select(pic => pic.Nama)
                .ToListAsync();
            ProductBacklogItems = await db.ScrumProductBacklogItems.AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new ScrumProductBacklogItemOption(
                    item.Id,
                    $"PBI-SMARE-{item.Id:000}",
                    item.Summary,
                    item.TargetSprint))
                .ToListAsync();
            Sprints = (await db.ScrumSprints.AsNoTracking()
                    .OrderByDescending(sprint => sprint.Status == "Berjalan")
                    .ThenByDescending(sprint => sprint.StartDate)
                    .ThenByDescending(sprint => sprint.Id)
                    .Select(sprint => new ScrumSprintRow(
                        sprint.Id,
                        sprint.ProductBacklogItem == null
                            ? sprint.Name
                            : $"PBI-SMARE-{sprint.ProductBacklogItem.Id:000}",
                        sprint.ProductBacklogItemId,
                        sprint.ProductBacklogItem == null
                            ? null
                            : $"PBI-SMARE-{sprint.ProductBacklogItem.Id:000}",
                        sprint.ProductBacklogItem == null
                            ? null
                            : sprint.ProductBacklogItem.Summary,
                        sprint.Focus,
                        sprint.StartDate,
                        sprint.EndDate,
                        sprint.Status))
                    .ToListAsync());
            SelectedSprintId = SelectSprintId(preferredSprintId, productBacklogItemId);

            if (SelectedSprintId is not { } sprintId)
            {
                Tasks = [];
                Blockers = [];
                NormalizePicFilter();
                return;
            }

            Tasks = (await db.ScrumTasks.AsNoTracking()
                    .Where(task => task.SprintId == sprintId)
                    .OrderBy(task => task.Id)
                    .Select(task => new ScrumTaskRow(
                        task.Id,
                        $"TSK-SMARE-{task.Id:000}",
                        task.SprintId,
                        task.Title,
                        task.Owner ?? string.Empty,
                        GetInitials(task.Owner),
                        task.Category,
                        task.Priority,
                        task.Status,
                        task.EstimatedHour,
                        task.Notes))
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
            NormalizePicFilter();
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

    public void FilterTasksByPic(ScrumPicFilterOption? pic)
    {
        if (pic is not null && !PicFilterOptions.Contains(pic))
        {
            snackbar.Add("Filter PIC tidak valid. Pilih PIC yang tersedia.", Severity.Warning);
            return;
        }

        SelectedPicFilter = pic ?? PicFilterOptions[0];
        NotifyStateChanged();
    }

    public async Task SaveSprintAsync(ScrumSprintInput input)
    {
        var focus = input.Focus.Trim();
        if (input.ProductBacklogItemId is not { } productBacklogItemId ||
            !ProductBacklogItems.Any(item => item.Id == productBacklogItemId) ||
            string.IsNullOrWhiteSpace(focus) || focus.Length > 250 ||
            input.EndDate.Date < input.StartDate.Date ||
            input.EndDate.Date > input.StartDate.Date.AddDays(13) ||
            !SprintStatuses.Contains(input.Status, StringComparer.Ordinal))
        {
            snackbar.Add("Periksa Product Backlog Item, fokus, rentang tanggal 14 hari, dan status sprint.",
                Severity.Warning);
            return;
        }

        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var productBacklogItem = await db.ScrumProductBacklogItems
                .SingleOrDefaultAsync(item => item.Id == productBacklogItemId)
                ?? throw new InvalidOperationException("Product Backlog Item tidak ditemukan.");
            var name = $"PBI-SMARE-{productBacklogItem.Id:000}";
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
                    ProductBacklogItemId = productBacklogItem.Id,
                    Focus = focus,
                    StartDate = input.StartDate.Date,
                    EndDate = input.EndDate.Date,
                    Status = input.Status
                };
                db.ScrumSprints.Add(entity);
            }

            entity.Name = name;
            entity.ProductBacklogItemId = productBacklogItem.Id;
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
        SprintDeleteMessage = null;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var sprint = await db.ScrumSprints.SingleOrDefaultAsync(item => item.Id == id);
            if (sprint is null)
            {
                SprintDeleteMessage = "Sprint tidak ditemukan. Daftar perlu dimuat ulang.";
                snackbar.Add(SprintDeleteMessage, Severity.Warning);
                return;
            }

            var taskCount = await db.ScrumTasks.CountAsync(task => task.SprintId == id);
            var blockerCount = await db.ScrumBlockers.CountAsync(blocker => blocker.SprintId == id);
            if (taskCount > 0 || blockerCount > 0)
            {
                SprintDeleteMessage =
                    $"Sprint tidak bisa dihapus karena masih memiliki {taskCount} task dan {blockerCount} blocker. " +
                    "Hapus task dan blocker tersebut terlebih dahulu.";
                snackbar.Add(SprintDeleteMessage, Severity.Warning);
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
            NotifyStateChanged();
        }
    }

    public async Task SaveTaskAsync(ScrumTaskInput input)
    {
        var title = input.Title.Trim();
        var owner = Clean(input.Owner);
        var category = Clean(input.Category);
        var notes = Clean(input.Notes);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 250 ||
            owner?.Length > 150 ||
            category is null || !TaskCategories.Contains(category, StringComparer.Ordinal) ||
            input.EstimatedHour is < 0 or > 999999.99m ||
            !TaskStatuses.Contains(input.Status, StringComparer.Ordinal) ||
            !Priorities.Contains(input.Priority, StringComparer.Ordinal) ||
            !Sprints.Any(sprint => sprint.Id == input.SprintId))
        {
            snackbar.Add("Periksa judul, PIC, kategori, estimasi jam, prioritas, status, dan sprint task.",
                Severity.Warning);
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
                    Category = category,
                    Priority = input.Priority,
                    Status = input.Status
                };
                db.ScrumTasks.Add(entity);
            }

            if (owner is not null &&
                !await db.MasterPics.AnyAsync(pic => pic.Nama == owner) &&
                !StringComparer.Ordinal.Equals(entity.Owner, owner))
            {
                snackbar.Add("PIC tidak lagi tersedia di Master PIC. Pilih PIC yang masih aktif.", Severity.Warning);
                await LoadAsync(input.SprintId);
                return;
            }

            entity.SprintId = input.SprintId;
            entity.Title = title;
            entity.Owner = owner;
            entity.Category = category;
            entity.Priority = input.Priority;
            entity.Status = input.Status;
            entity.EstimatedHour = input.EstimatedHour;
            entity.Notes = notes;
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

    private int? SelectSprintId(int? preferredSprintId, int? productBacklogItemId)
    {
        if (preferredSprintId is { } requestedId &&
            Sprints.Any(sprint => sprint.Id == requestedId))
        {
            return requestedId;
        }

        if (productBacklogItemId is { } requestedProductBacklogItemId)
        {
            return Sprints
                .Where(sprint => sprint.ProductBacklogItemId == requestedProductBacklogItemId)
                .OrderByDescending(sprint => sprint.Status == "Berjalan")
                .ThenByDescending(sprint => sprint.StartDate)
                .ThenByDescending(sprint => sprint.Id)
                .Select(sprint => (int?)sprint.Id)
                .FirstOrDefault();
        }

        return Sprints.FirstOrDefault(sprint => sprint.Status == "Berjalan")?.Id
            ?? Sprints.FirstOrDefault()?.Id;
    }

    private void NormalizePicFilter()
    {
        if (SelectedPicFilter.Owner is not { Length: > 0 } selectedPic)
        {
            return;
        }

        SelectedPicFilter = PicFilterOptions.FirstOrDefault(option =>
            option.Owner is { Length: > 0 } owner &&
            string.Equals(owner, selectedPic, StringComparison.OrdinalIgnoreCase))
            ?? PicFilterOptions[0];
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
