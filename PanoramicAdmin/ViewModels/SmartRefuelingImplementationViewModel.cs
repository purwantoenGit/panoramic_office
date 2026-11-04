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
    ILogger<SmartRefuelingImplementationViewModel> logger,
    PitStopMappingService pitStopMappingService,
    RfidEquipmentService rfidEquipmentService)
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

    public async Task<SmartRefuelingLiveProgress> GetLiveProgressAsync(string site)
    {
        var snapshot = await LoadSnapshotAsync();
        return BuildProgress(snapshot, site);
    }

    public async Task<IReadOnlyDictionary<string, double?>> GetAchievementsAsync(IEnumerable<string> sites)
    {
        var snapshot = await LoadSnapshotAsync();
        return sites
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                site => site,
                site => BuildProgress(snapshot, site).Achievement,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<EquipmentRequirementRow>> GetEquipmentRequirementsAsync(
        IEnumerable<string> sites)
    {
        var snapshot = await LoadSnapshotAsync();
        await using var manualDb = await dbContextFactory.CreateDbContextAsync();
        var manualBySite = new Dictionary<string, SmartRefuelingEquipmentActual>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var item in await manualDb.SmartRefuelingEquipmentActuals.AsNoTracking().ToListAsync())
            {
                manualBySite[item.Site] = item;
            }
        }
        catch (SqlException exception) when (exception.Number is 207 or 208)
        {
            logger.LogWarning(exception,
                "Table pano_smart_refueling_equipment_actual is missing or outdated; run its SQL scripts.");
            snackbar.Add(
                "Tabel Act (Manual) belum dibuat/diperbarui. Jalankan Create_SmartRefuelingEquipmentActual.sql atau Alter_SmartRefuelingEquipmentActual_AddKet.sql.",
                Severity.Warning);
        }
        return sites
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(site =>
            {
                var pitStopCount = snapshot.PitStop.Count(item => IsSite(item.District, site));
                var fuelTruckCount = snapshot.FuelTruck.Count(item => IsSite(item.District, site));
                var pitStop = snapshot.PitStop.Where(item => IsSite(item.District, site)).ToList();
                var fuelTruck = snapshot.FuelTruck.Where(item => IsSite(item.District, site)).ToList();
                var equipment = snapshot.Equipment.Where(item => IsSite(item.District, site)).ToList();
                var totalUnits = equipment.Sum(item => item.UnitCount);
                var hdUnits = equipment
                    .Where(item => string.Equals(item.EquipClass, "DT", StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.UnitCount);
                var onlyPitStop = pitStopCount > 0 && fuelTruckCount == 0;
                var siteInputs = snapshot.RfidUnits
                    .Where(item => IsSite(item.Key.District, site))
                    .ToList();
                var totalActive = siteInputs.Sum(item => item.Value);
                var hdActive = siteInputs
                    .Where(item => string.Equals(item.Key.EquipClass, "DT", StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.Value);
                var antiMetalPlan = pitStopCount == 0 ? hdUnits : 0;
                var segelPlan = onlyPitStop ? totalUnits - hdUnits : totalUnits;
                var antiMetalAct = pitStopCount == 0 ? hdActive : 0;
                var segelAct = onlyPitStop ? totalActive - hdActive : totalActive;
                return new EquipmentRequirementRow(
                    site,
                    pitStopCount,
                    fuelTruckCount,
                    antiMetalPlan,
                    null,
                    segelPlan,
                    pitStop.Count(item => item.HasRfid),
                    fuelTruck.Count(item => item.HasRfid),
                    Math.Min(antiMetalAct, antiMetalPlan),
                    null,
                    Math.Min(segelAct, segelPlan),
                    manualBySite.TryGetValue(site, out var manual)
                        ? new EquipmentManualValues(
                            manual.PitStop, manual.FuelTruck, manual.TagAntiMetal,
                            manual.TagSticker, manual.TagSegel,
                            manual.KetPitStop, manual.KetFuelTruck, manual.KetTagAntiMetal,
                            manual.KetTagSticker, manual.KetTagSegel)
                        : new EquipmentManualValues(null, null, null, null, null));
            })
            .ToList();
    }

    private static bool IsSite(string district, string site) =>
        string.Equals(district, site, StringComparison.OrdinalIgnoreCase);

    private async Task<LiveSnapshot> LoadSnapshotAsync()
    {
        var pitStopTask = pitStopMappingService.GetPitStopStatusesAsync(7);
        var fuelTruckTask = pitStopMappingService.GetFuelTruckStatusesAsync(7);
        var equipmentTask = rfidEquipmentService.SearchAsync(null);
        var rfidUnitsTask = rfidEquipmentService.GetRfidInputCountsAsync();
        await Task.WhenAll(pitStopTask, fuelTruckTask, equipmentTask, rfidUnitsTask);
        return new LiveSnapshot(
            pitStopTask.Result, fuelTruckTask.Result, equipmentTask.Result, rfidUnitsTask.Result);
    }

    private static SmartRefuelingLiveProgress BuildProgress(LiveSnapshot snapshot, string site)
    {
        var pitStop = snapshot.PitStop
            .Where(item => string.Equals(item.District, site, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var fuelTruck = snapshot.FuelTruck
            .Where(item => string.Equals(item.District, site, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var equipment = snapshot.Equipment
            .Where(item => string.Equals(item.District, site, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var rfidUnitCount = snapshot.RfidUnits
            .Where(item => string.Equals(item.Key.District, site, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Value);

        return new SmartRefuelingLiveProgress(
            new SmartRefuelingProgressMetric(pitStop.Count(item => item.HasRfid), pitStop.Count),
            new SmartRefuelingProgressMetric(fuelTruck.Count(item => item.HasRfid), fuelTruck.Count),
            new SmartRefuelingProgressMetric(rfidUnitCount, equipment.Sum(item => item.UnitCount)));
    }

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

    private static string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public async Task<IReadOnlyDictionary<(string Site, string Key), CurveProgressValue>> GetCurveProgressAsync()
    {
        var result = new Dictionary<(string, string), CurveProgressValue>();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            foreach (var item in await db.SmartRefuelingCurveProgresses.AsNoTracking().ToListAsync())
            {
                result[(item.Site.ToUpperInvariant(), item.ItemKey)] = new CurveProgressValue(item.Percent, item.UpdatedOn);
            }
        }
        catch (SqlException exception) when (exception.Number is 207 or 208)
        {
            logger.LogWarning(exception, "Table pano_smart_refueling_curve_progress is missing; run its SQL script.");
            snackbar.Add("Tabel progres Curve belum dibuat. Jalankan Create_SmartRefuelingCurveProgress.sql.", Severity.Warning);
        }
        return result;
    }

    public async Task<bool> SaveCurveProgressAsync(string site, string key, int percent)
    {
        if (percent is < 0 or > 100)
        {
            return false;
        }

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var entity = await db.SmartRefuelingCurveProgresses
                .SingleOrDefaultAsync(item => item.Site == site && item.ItemKey == key);
            if (entity is null)
            {
                entity = new SmartRefuelingCurveProgress { Site = site, ItemKey = key };
                db.SmartRefuelingCurveProgresses.Add(entity);
            }

            entity.Percent = percent;
            entity.UpdatedOn = DateTime.Today;
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception exception)
        {
            HandleError(exception, "menyimpan progres Curve Project", "Progres gagal disimpan.");
            return false;
        }
    }

    public async Task<bool> SaveManualActualAsync(string site, EquipmentManualValues values)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var entity = await db.SmartRefuelingEquipmentActuals
                .SingleOrDefaultAsync(item => item.Site == site);
            if (entity is null)
            {
                entity = new SmartRefuelingEquipmentActual { Site = site };
                db.SmartRefuelingEquipmentActuals.Add(entity);
            }

            entity.PitStop = values.PitStop;
            entity.FuelTruck = values.FuelTruck;
            entity.TagAntiMetal = values.TagAntiMetal;
            entity.TagSticker = values.TagSticker;
            entity.TagSegel = values.TagSegel;
            entity.KetPitStop = Clean(values.KetPitStop);
            entity.KetFuelTruck = Clean(values.KetFuelTruck);
            entity.KetTagAntiMetal = Clean(values.KetTagAntiMetal);
            entity.KetTagSticker = Clean(values.KetTagSticker);
            entity.KetTagSegel = Clean(values.KetTagSegel);
            await db.SaveChangesAsync();
            snackbar.Add("Act (Manual) berhasil disimpan.", Severity.Success);
            return true;
        }
        catch (Exception exception)
        {
            HandleError(exception, "menyimpan Act (Manual) Equipment Requirement",
                "Act (Manual) gagal disimpan.");
            return false;
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

public sealed record EquipmentRequirementRow(
    string Site,
    int PitStopCount,
    int FuelTruckCount,
    int TagAntiMetal,
    int? TagSticker,
    int TagSegel,
    int PitStopAct,
    int FuelTruckAct,
    int TagAntiMetalAct,
    int? TagStickerAct,
    int TagSegelAct,
    EquipmentManualValues Manual);

public sealed record CurveProgressValue(int Percent, DateTime UpdatedOn);

public sealed record EquipmentManualValues(
    int? PitStop,
    int? FuelTruck,
    int? TagAntiMetal,
    int? TagSticker,
    int? TagSegel,
    string? KetPitStop = null,
    string? KetFuelTruck = null,
    string? KetTagAntiMetal = null,
    string? KetTagSticker = null,
    string? KetTagSegel = null);

internal sealed record LiveSnapshot(
    IReadOnlyList<PitStopWarehouseStatus> PitStop,
    IReadOnlyList<PitStopWarehouseStatus> FuelTruck,
    IReadOnlyList<RfidEquipmentGroup> Equipment,
    IReadOnlyDictionary<RfidManualUnitKey, int> RfidUnits);
