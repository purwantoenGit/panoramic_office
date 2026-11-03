using Microsoft.Data.SqlClient;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class PitStopMappingViewModel(
    PitStopMappingService mappingService,
    ILogger<PitStopMappingViewModel> logger)
{
    public IReadOnlyList<string> WarehouseIds { get; private set; } = [];
    public IReadOnlyList<PitStopDistrictRow> Districts { get; private set; } = [];
    public IReadOnlyList<PitStopDistrictRow> FilteredDistricts { get; private set; } = [];
    public IReadOnlyList<string> FuelTruckWarehouseIds { get; private set; } = [];
    public IReadOnlyList<PitStopDistrictRow> FuelTruckDistricts { get; private set; } = [];
    public IReadOnlyList<PitStopDistrictRow> FilteredFuelTruckDistricts { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public int SelectedDays { get; private set; } = 7;
    public string? LoadError { get; private set; }
    public string? FuelTruckLoadError { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsFuelTruckLoading { get; private set; }
    public DateTime? LastUpdated { get; private set; }
    public DateTime? FuelTruckLastUpdated { get; private set; }
    public int DistrictCount => Districts.Count;
    public int TotalDst => Districts.Sum(district => district.Dst);
    public decimal RfidInstalled => GetRfidPercentage(Districts);
    public int FuelTruckDistrictCount => FuelTruckDistricts.Count;
    public int FuelTruckTotalDst => FuelTruckDistricts.Sum(district => district.Dst);
    public decimal FuelTruckRfidInstalled => GetRfidPercentage(FuelTruckDistricts);

    public async Task LoadAllAsync(int? days = null)
    {
        SetDays(days);
        await Task.WhenAll(LoadPitStopAsync(), LoadFuelTruckAsync());
    }

    public async Task LoadPitStopAsync(int? days = null)
    {
        SetDays(days);
        IsLoading = true;
        LoadError = null;
        try
        {
            var statuses = await mappingService.GetPitStopStatusesAsync(SelectedDays);
            (WarehouseIds, Districts) = await BuildRowsAsync(statuses, "FP");
            LastUpdated = DateTime.Now;
            ApplySearch();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Failed to load Pit Stop mapping data from RfidDb.");
            LoadError = "Data Pit Stop gagal dimuat. Periksa koneksi RfidDb dan hak akses SELECT ke DB_SATUPAMA_SM.dbo.tbl_t_log_sheet_detail.";
            ClearPitStopData();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The RfidDb connection string is unavailable.");
            LoadError = "Koneksi RfidDb belum dikonfigurasi. Periksa User Secrets atau environment variable.";
            ClearPitStopData();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected error while loading Pit Stop mapping data.");
            LoadError = "Terjadi kesalahan saat memuat data Pit Stop. Periksa log aplikasi.";
            ClearPitStopData();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadFuelTruckAsync(int? days = null)
    {
        SetDays(days);
        IsFuelTruckLoading = true;
        FuelTruckLoadError = null;
        try
        {
            var statuses = await mappingService.GetFuelTruckStatusesAsync(SelectedDays);
            (FuelTruckWarehouseIds, FuelTruckDistricts) = await BuildRowsAsync(statuses, "FT");
            FuelTruckLastUpdated = DateTime.Now;
            ApplySearch();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Failed to load Fuel Truck mapping data from RfidDb.");
            FuelTruckLoadError = "Data Fuel Truck gagal dimuat. Periksa koneksi RfidDb dan hak akses SELECT ke DB_SATUPAMA_SM.dbo.tbl_t_log_sheet_detail.";
            ClearFuelTruckData();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The RfidDb connection string is unavailable.");
            FuelTruckLoadError = "Koneksi RfidDb belum dikonfigurasi. Periksa User Secrets atau environment variable.";
            ClearFuelTruckData();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected error while loading Fuel Truck mapping data.");
            FuelTruckLoadError = "Terjadi kesalahan saat memuat data Fuel Truck. Periksa log aplikasi.";
            ClearFuelTruckData();
        }
        finally
        {
            IsFuelTruckLoading = false;
        }
    }

    public void Search(string? text)
    {
        SearchText = text ?? string.Empty;
        ApplySearch();
    }

    public decimal GetFilteredRfidInstalled() => GetRfidPercentage(FilteredDistricts);

    public decimal GetFilteredFuelTruckRfidInstalled() =>
        GetRfidPercentage(FilteredFuelTruckDistricts);

    private async Task<(IReadOnlyList<string> WarehouseIds, IReadOnlyList<PitStopDistrictRow> Districts)>
        BuildRowsAsync(
            IReadOnlyList<PitStopWarehouseStatus> statuses,
            string warehousePrefix)
    {
        if (statuses.Count == 0)
        {
            var fallbackDistricts = await mappingService.GetFallbackDistrictsAsync(warehousePrefix);
            return (
                [],
                fallbackDistricts.Select(district => new PitStopDistrictRow(
                    district,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    0,
                    0)).ToArray());
        }

        var warehouseIds = statuses
            .Select(status => status.WarehouseId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var districts = statuses
            .GroupBy(status => status.District, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var warehouseStatuses = group.ToDictionary(
                    status => status.WarehouseId,
                    status => status.HasRfid
                        ? "RFID"
                        : status.HasNonRfid ? "MANUAL" : "-",
                    StringComparer.OrdinalIgnoreCase);
                var rfidCount = group.Count(status => status.HasRfid);
                var manualOnlyCount = group.Count(status => !status.HasRfid && status.HasNonRfid);
                return new PitStopDistrictRow(
                    group.Key,
                    warehouseStatuses,
                    rfidCount,
                    manualOnlyCount);
            })
            .OrderBy(district => district.District, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return (warehouseIds, districts);
    }

    private void SetDays(int? days)
    {
        if (days.HasValue)
        {
            SelectedDays = days.Value;
        }
    }

    private void ApplySearch()
    {
        FilteredDistricts = FilterDistricts(Districts);
        FilteredFuelTruckDistricts = FilterDistricts(FuelTruckDistricts);
    }

    private IReadOnlyList<PitStopDistrictRow> FilterDistricts(
        IReadOnlyList<PitStopDistrictRow> districts) =>
        string.IsNullOrWhiteSpace(SearchText)
            ? districts
            : districts
                .Where(district => district.District.Contains(
                    SearchText.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

    private static decimal GetRfidPercentage(IReadOnlyList<PitStopDistrictRow> districts)
    {
        var total = districts.Sum(district => district.Dst);
        return total == 0
            ? 0
            : decimal.Round(
                (decimal)districts.Sum(district => district.RfidCount) / total * 100,
                2);
    }

    private void ClearPitStopData()
    {
        WarehouseIds = [];
        Districts = [];
        FilteredDistricts = [];
        LastUpdated = null;
    }

    private void ClearFuelTruckData()
    {
        FuelTruckWarehouseIds = [];
        FuelTruckDistricts = [];
        FilteredFuelTruckDistricts = [];
        FuelTruckLastUpdated = null;
    }
}
