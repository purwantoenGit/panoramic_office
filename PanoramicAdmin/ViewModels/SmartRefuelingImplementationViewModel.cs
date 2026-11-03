using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class SmartRefuelingImplementationViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<SmartRefuelingImplementationViewModel> logger)
{
    public IReadOnlyList<SmartRefuelingImplementationRow> Items { get; private set; } = [];
    public IReadOnlyList<string> ActiveSites { get; private set; } = [];
    public int DoneCount => CountStatus("Done");
    public int ProgressCount => CountStatus("Progress");
    public int OverdueCount => CountStatus("Overdue");
    public int PlanCount => CountStatus("Plan");
    public int UnplannedCount => CountStatus("Unplanned");
    public string? DatabaseError { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsSaving { get; private set; }

    private int CountStatus(string status) =>
        Items.Sum(item => item.Statuses.Count(value => value == status));

    public async Task LoadAsync()
    {
        IsLoading = true;
        DatabaseError = null;
        try
        {
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("PanoramicDb")))
            {
                DatabaseError = "Konfigurasi koneksi database PanoramicDb belum tersedia.";
                logger.LogError("The PanoramicDb connection string is missing or empty.");
                return;
            }

            await using var db = await dbContextFactory.CreateDbContextAsync();
            ActiveSites = await db.MasterSites.AsNoTracking()
                .Where(site => site.Status == "Active")
                .OrderBy(site => site.Sites)
                .Select(site => site.Sites)
                .ToListAsync();
            Items = (await db.SmartRefuelingImplementations.AsNoTracking()
                    .ToListAsync())
                .Select(ToRow)
                .OrderBy(GetFirstFilledPeriodIndex)
                .ThenBy(item => item.Site)
                .ThenBy(item => item.Activity)
                .ToList();
        }
        catch (Exception exception)
        {
            HandleError(exception, "memuat roadmap implementasi Smart Refueling",
                "Data implementasi Smart Refueling gagal dimuat.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SaveAsync(SmartRefuelingImplementationInput input)
    {
        var site = input.Site.Trim();
        var activity = input.Activity.Trim();
        if (string.IsNullOrWhiteSpace(site) || site.Length > 150 ||
            !ActiveSites.Contains(site, StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(activity) || activity.Length > 250 ||
            input.Statuses.Count != SmartRefuelingImplementationCatalog.Periods.Count ||
            input.Statuses.Any(status =>
                !string.IsNullOrWhiteSpace(status) &&
                !SmartRefuelingImplementationCatalog.Statuses.Contains(status, StringComparer.Ordinal)))
        {
            snackbar.Add("Pilih site aktif dari Master Sites dan periksa aktivitas serta status setiap periode.", Severity.Warning);
            return;
        }

        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            SmartRefuelingImplementation entity;
            if (input.Id is { } id)
            {
                entity = await db.SmartRefuelingImplementations.SingleOrDefaultAsync(item => item.Id == id)
                    ?? throw new InvalidOperationException("Data implementasi tidak ditemukan.");
            }
            else
            {
                entity = new SmartRefuelingImplementation
                {
                    Site = site,
                    Activity = activity
                };
                db.SmartRefuelingImplementations.Add(entity);
            }

            entity.Site = site;
            entity.Activity = activity;
            SetStatuses(entity, input.Statuses);
            await db.SaveChangesAsync();
            snackbar.Add("Data implementasi Smart Refueling berhasil disimpan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, "menyimpan implementasi Smart Refueling",
                "Data implementasi gagal disimpan. Periksa data dan coba lagi.");
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
            var entity = await db.SmartRefuelingImplementations.SingleOrDefaultAsync(item => item.Id == id);
            if (entity is null)
            {
                snackbar.Add("Data implementasi tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            db.SmartRefuelingImplementations.Remove(entity);
            await db.SaveChangesAsync();
            snackbar.Add("Data implementasi berhasil dihapus.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, "menghapus implementasi Smart Refueling",
                "Data implementasi gagal dihapus.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private static SmartRefuelingImplementationRow ToRow(SmartRefuelingImplementation entity) =>
        new(entity.Id, entity.Site, entity.Activity,
        [
            entity.Status2026Q1,
            entity.Status2026Q2,
            entity.Status2026Q3,
            entity.Status2026Q4,
            entity.Status2027Q1,
            entity.Status2027Q2,
            entity.Status2027Q3,
            entity.Status2027Q4
        ]);

    private static int GetFirstFilledPeriodIndex(SmartRefuelingImplementationRow item)
    {
        for (var index = 0; index < item.Statuses.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(item.Statuses[index]))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static void SetStatuses(SmartRefuelingImplementation entity, IReadOnlyList<string?> statuses)
    {
        entity.Status2026Q1 = Normalize(statuses[0]);
        entity.Status2026Q2 = Normalize(statuses[1]);
        entity.Status2026Q3 = Normalize(statuses[2]);
        entity.Status2026Q4 = Normalize(statuses[3]);
        entity.Status2027Q1 = Normalize(statuses[4]);
        entity.Status2027Q2 = Normalize(statuses[5]);
        entity.Status2027Q3 = Normalize(statuses[6]);
        entity.Status2027Q4 = Normalize(statuses[7]);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
}

public sealed record SmartRefuelingImplementationRow(
    int Id,
    string Site,
    string Activity,
    IReadOnlyList<string?> Statuses);

public sealed record SmartRefuelingImplementationInput(
    int? Id,
    string Site,
    string Activity,
    IReadOnlyList<string?> Statuses);
