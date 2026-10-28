using Microsoft.Data.SqlClient;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class RfidTagInstallViewModel(
    RfidEquipmentService equipmentService,
    RfidManualUnitService manualUnitService,
    ISnackbar snackbar,
    ILogger<RfidTagInstallViewModel> logger)
{
    private static readonly (string Code, string Description)[] EquipClassDefinitions =
    [
        ("DT", "Dump Truck"),
        ("EX", "Excavator"),
        ("GR", "Grader"),
        ("DZ", "Dozer")
    ];

    private static readonly string[] PriorityDistrictOrder =
    [
        "BTSJ",
        "KPCS",
        "KIDE",
        "MTBU",
        "BRCG",
        "VIPO",
        "BRCB",
        "SMMS"
    ];

    private IReadOnlyList<RfidDistrictRow> _allDistrictRows = [];

    public IReadOnlyList<RfidEquipClassGroup> EquipClassGroups { get; private set; } = [];
    public IReadOnlyList<string> DistrictOptions { get; private set; } = [];
    public IReadOnlyCollection<string> SelectedDistricts { get; private set; } = [];
    public IReadOnlyList<RfidDistrictRow> Districts { get; private set; } = [];
    public IReadOnlyDictionary<RfidManualUnitKey, int> ManualCounts { get; private set; } =
        new Dictionary<RfidManualUnitKey, int>();
    public IReadOnlyDictionary<RfidManualUnitKey, int> RfidInputCounts { get; private set; } =
        new Dictionary<RfidManualUnitKey, int>();
    public string SearchText { get; private set; } = string.Empty;
    public string? LoadError { get; private set; }
    public bool IsLoading { get; private set; }
    private readonly Dictionary<RfidManualUnitKey, int?> _editValues = [];
    private readonly HashSet<RfidManualUnitKey> _savingCells = [];
    private bool _rfidInputCountsLoaded;

    public async Task LoadAsync(string? searchText = null)
    {
        SearchText = searchText ?? string.Empty;
        IsLoading = true;
        LoadError = null;

        try
        {
            var groups = await equipmentService.SearchAsync(SearchText);
            ManualCounts = await manualUnitService.GetCountsAsync();
            if (!_rfidInputCountsLoaded)
            {
                RfidInputCounts = await equipmentService.GetRfidInputCountsAsync();
                _rfidInputCountsLoaded = true;
            }

            EquipClassGroups = EquipClassDefinitions
                .Select(definition => new RfidEquipClassGroup(
                    definition.Code,
                    definition.Description,
                    groups
                        .Where(group => group.EquipClass == definition.Code)
                        .Select(group => group.Egi)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(egi => egi, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(egi => egi, StringComparer.Ordinal)
                        .ToArray()))
                .Where(group => group.EgiColumns.Count > 0)
                .ToArray();
            SelectedDistricts = SelectedDistricts
                .Where(IsDisplayedDistrict)
                .ToArray();
            _allDistrictRows = groups
                .Where(group => IsDisplayedDistrict(group.District))
                .GroupBy(group => group.District, StringComparer.Ordinal)
                .Select(group => new RfidDistrictRow(
                    group.Key,
                    group.ToDictionary(
                        item => new RfidPivotKey(item.EquipClass, item.Egi),
                        item => item.UnitCount)))
                .OrderBy(district => GetDistrictPriority(district.District))
                .ThenBy(district => district.District, StringComparer.OrdinalIgnoreCase)
                .ThenBy(district => district.District, StringComparer.Ordinal)
                .ToArray();
            DistrictOptions = OrderDistrictNames(
                _allDistrictRows.Select(district => district.District).Concat(SelectedDistricts));
            ApplyDistrictFilter();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Failed to read equipment or RFID log data from RfidDb.");
            LoadError = "Data RFID gagal dimuat. Periksa koneksi RfidDb dan hak akses SELECT ke tbl_r_equip serta tbl_t_log_sheet_detail.";
            EquipClassGroups = [];
            _allDistrictRows = [];
            DistrictOptions = [];
            Districts = [];
            ManualCounts = new Dictionary<RfidManualUnitKey, int>();
            RfidInputCounts = new Dictionary<RfidManualUnitKey, int>();
            _rfidInputCountsLoaded = false;
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The RfidDb connection string is unavailable.");
            LoadError = "Koneksi RfidDb belum dikonfigurasi. Periksa User Secrets atau environment variable.";
            EquipClassGroups = [];
            _allDistrictRows = [];
            DistrictOptions = [];
            Districts = [];
            ManualCounts = new Dictionary<RfidManualUnitKey, int>();
            RfidInputCounts = new Dictionary<RfidManualUnitKey, int>();
            _rfidInputCountsLoaded = false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected error while reading dbo.tbl_r_equip.");
            LoadError = "Terjadi kesalahan saat memuat data RFID. Periksa log aplikasi.";
            EquipClassGroups = [];
            _allDistrictRows = [];
            DistrictOptions = [];
            Districts = [];
            ManualCounts = new Dictionary<RfidManualUnitKey, int>();
            RfidInputCounts = new Dictionary<RfidManualUnitKey, int>();
            _rfidInputCountsLoaded = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SetSelectedDistricts(IReadOnlyCollection<string>? districts)
    {
        SelectedDistricts = districts?
            .Where(IsDisplayedDistrict)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        DistrictOptions = OrderDistrictNames(DistrictOptions.Concat(SelectedDistricts));
        ApplyDistrictFilter();
    }

    public int? GetManualCount(RfidManualUnitKey key)
    {
        if (ManualCounts.TryGetValue(key, out var count))
        {
            return count;
        }

        if (key.EquipClass != "DT" || key.Egi != "A60H/A60J")
        {
            return null;
        }

        var a60hExists = ManualCounts.TryGetValue(
            new RfidManualUnitKey(key.District, key.EquipClass, "A60H"),
            out var a60hCount);
        var a60jExists = ManualCounts.TryGetValue(
            new RfidManualUnitKey(key.District, key.EquipClass, "A60J"),
            out var a60jCount);
        return a60hExists || a60jExists ? a60hCount + a60jCount : null;
    }

    public int GetRfidInputCount(string district, string equipClass, string egi) =>
        RfidInputCounts.GetValueOrDefault(
            new RfidManualUnitKey(district, equipClass, egi));

    public bool IsEditing(RfidManualUnitKey key) => _editValues.ContainsKey(key);

    public int? GetEditValue(RfidManualUnitKey key) =>
        _editValues.TryGetValue(key, out var value) ? value : null;

    public bool IsSaving(RfidManualUnitKey key) => _savingCells.Contains(key);

    public void BeginEdit(RfidManualUnitKey key) =>
        _editValues[key] = GetManualCount(key);

    public void SetEditValue(RfidManualUnitKey key, int? value)
    {
        if (_editValues.ContainsKey(key))
        {
            _editValues[key] = value;
        }
    }

    public void CancelEdit(RfidManualUnitKey key) => _editValues.Remove(key);

    public async Task SaveManualCountAsync(RfidManualUnitKey key)
    {
        if (!_editValues.TryGetValue(key, out var value) || _savingCells.Contains(key))
        {
            return;
        }

        if (value < 0)
        {
            snackbar.Add("Jumlah tambahan tidak boleh negatif.", Severity.Warning);
            return;
        }

        _savingCells.Add(key);
        try
        {
            await manualUnitService.SaveAsync(key, value);
            var updatedCounts = ManualCounts.ToDictionary();
            if (key.EquipClass == "DT" && key.Egi == "A60H/A60J")
            {
                updatedCounts.Remove(new RfidManualUnitKey(key.District, key.EquipClass, "A60H"));
                updatedCounts.Remove(new RfidManualUnitKey(key.District, key.EquipClass, "A60J"));
            }

            if (value is int count)
            {
                updatedCounts[key] = count;
            }
            else
            {
                updatedCounts.Remove(key);
            }

            ManualCounts = updatedCounts;
            _editValues.Remove(key);
            snackbar.Add("Jumlah tambahan berhasil disimpan.", Severity.Success);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to save additional RFID count for {District}, {EquipClass}, {Egi}.",
                key.District,
                key.EquipClass,
                key.Egi);
            snackbar.Add("Jumlah tambahan gagal disimpan. Periksa koneksi db_panoramic.", Severity.Error);
        }
        finally
        {
            _savingCells.Remove(key);
        }
    }

    private void ApplyDistrictFilter()
    {
        Districts = SelectedDistricts.Count == 0
            ? _allDistrictRows
            : _allDistrictRows
                .Where(district => SelectedDistricts.Contains(district.District, StringComparer.Ordinal))
                .ToArray();
    }

    private static bool IsDisplayedDistrict(string district) =>
        !string.Equals(district.Trim(), "ERKA", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> OrderDistrictNames(IEnumerable<string> districts) =>
        districts
            .Distinct(StringComparer.Ordinal)
            .OrderBy(GetDistrictPriority)
            .ThenBy(district => district, StringComparer.OrdinalIgnoreCase)
            .ThenBy(district => district, StringComparer.Ordinal)
            .ToArray();

    private static int GetDistrictPriority(string district)
    {
        var index = Array.FindIndex(
            PriorityDistrictOrder,
            priority => string.Equals(priority, district.Trim(), StringComparison.OrdinalIgnoreCase));
        return index < 0 ? PriorityDistrictOrder.Length : index;
    }
}

public sealed record RfidEquipClassGroup(
    string Code,
    string Description,
    IReadOnlyList<string> EgiColumns);

public readonly record struct RfidPivotKey(string EquipClass, string Egi);

public sealed record RfidDistrictRow(
    string District,
    IReadOnlyDictionary<RfidPivotKey, int> Counts);
