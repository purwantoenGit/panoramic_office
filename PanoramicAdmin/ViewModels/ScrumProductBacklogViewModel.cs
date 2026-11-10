using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class ScrumProductBacklogViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<ScrumProductBacklogViewModel> logger)
{
    public event Action? StateChanged;
    public IReadOnlyList<ScrumProductBacklogItemRow> Items { get; private set; } = [];
    public bool IsLoading { get; private set; } = true;
    public bool IsSaving { get; private set; }
    public string? DatabaseError { get; private set; }

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
            var sprintProgressByPbi = (await db.ScrumSprints.AsNoTracking()
                    .Where(sprint => sprint.ProductBacklogItemId.HasValue)
                    .Select(sprint => new
                    {
                        ProductBacklogItemId = sprint.ProductBacklogItemId.GetValueOrDefault(),
                        sprint.Id,
                        sprint.Status,
                        sprint.StartDate,
                        TaskCount = sprint.Tasks.Count(),
                        CompletedTaskCount = sprint.Tasks.Count(task => task.Status == "Selesai")
                    })
                    .ToListAsync())
                .GroupBy(sprint => sprint.ProductBacklogItemId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var sprint = group
                            .OrderByDescending(item => item.Status == "Berjalan")
                            .ThenByDescending(item => item.StartDate)
                            .ThenByDescending(item => item.Id)
                            .First();
                        return sprint.TaskCount == 0
                            ? 0
                            : (double)sprint.CompletedTaskCount / sprint.TaskCount * 100;
                    });
            var items = await db.ScrumProductBacklogItems.AsNoTracking()
                    .OrderBy(item => item.Id)
                    .Select(item => new ScrumProductBacklogItemRow(
                        item.Id,
                        $"PBI-SMARE-{item.Id:000}",
                        item.Module,
                        item.Summary,
                        item.AcceptanceCriteria,
                        item.Priority,
                        item.TargetSprint,
                        item.DefinitionOfDone,
                        null))
                    .ToListAsync();
            Items = items.Select(item =>
                sprintProgressByPbi.TryGetValue(item.Id, out var percentage)
                    ? item with { SprintCompletionPercent = percentage }
                    : item).ToList();
        }
        catch (Exception exception)
        {
            HandleError(exception, "memuat Product Backlog Item",
                "Data Product Backlog Item gagal dimuat.");
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task SaveAsync(ScrumProductBacklogItemInput input)
    {
        var module = input.Module.Trim();
        var summary = input.Summary.Trim();
        var acceptanceCriteria = input.AcceptanceCriteria.Trim();
        var definitionOfDone = input.DefinitionOfDone.Trim();
        if (string.IsNullOrWhiteSpace(module) || module.Length > 100 ||
            string.IsNullOrWhiteSpace(summary) || summary.Length > 250 ||
            string.IsNullOrWhiteSpace(acceptanceCriteria) ||
            string.IsNullOrWhiteSpace(definitionOfDone) ||
            !ScrumProductBacklogCatalog.Priorities.Contains(input.Priority, StringComparer.Ordinal) ||
            input.TargetSprint is < 1)
        {
            snackbar.Add("Periksa module, summary, AC, prioritas, target sprint, dan DoD PBI.",
                Severity.Warning);
            return;
        }

        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            ScrumProductBacklogItem entity;
            if (input.Id is { } id)
            {
                entity = await db.ScrumProductBacklogItems.SingleOrDefaultAsync(item => item.Id == id)
                    ?? throw new InvalidOperationException("Product Backlog Item tidak ditemukan.");
            }
            else
            {
                entity = new ScrumProductBacklogItem
                {
                    Module = module,
                    Summary = summary,
                    AcceptanceCriteria = acceptanceCriteria,
                    Priority = input.Priority,
                    Status = "Belum dimulai",
                    TargetSprint = input.TargetSprint,
                    DefinitionOfDone = definitionOfDone
                };
                db.ScrumProductBacklogItems.Add(entity);
            }

            entity.Module = module;
            entity.Summary = summary;
            entity.AcceptanceCriteria = acceptanceCriteria;
            entity.Priority = input.Priority;
            entity.TargetSprint = input.TargetSprint;
            entity.DefinitionOfDone = definitionOfDone;
            await db.SaveChangesAsync();
            snackbar.Add("Product Backlog Item berhasil disimpan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, "menyimpan Product Backlog Item",
                "Product Backlog Item gagal disimpan.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task DeleteAsync(int id)
    {
        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var item = await db.ScrumProductBacklogItems.SingleOrDefaultAsync(pbi => pbi.Id == id);
            if (item is null)
            {
                snackbar.Add("Product Backlog Item tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            if (await db.ScrumSprints.AnyAsync(sprint => sprint.ProductBacklogItemId == id))
            {
                snackbar.Add(
                    "Product Backlog Item tidak dapat dihapus karena masih digunakan oleh Scrum Sprint.",
                    Severity.Warning);
                return;
            }

            db.ScrumProductBacklogItems.Remove(item);
            await db.SaveChangesAsync();
            snackbar.Add("Product Backlog Item berhasil dihapus.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, "menghapus Product Backlog Item",
                "Product Backlog Item gagal dihapus.");
        }
        finally
        {
            IsSaving = false;
        }
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
