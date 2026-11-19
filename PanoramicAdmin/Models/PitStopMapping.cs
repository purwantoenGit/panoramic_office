namespace PanoramicAdmin.Models;

public sealed record PitStopWarehouseStatus(
    string District,
    string WarehouseId,
    bool HasRfid,
    bool HasNonRfid);

public sealed record FuelTruckRfidDailyAchievement(
    DateTime AchievementDate,
    long ManualTransactionCount,
    long RfidCount);

public sealed record FuelTruckDistrictSummary(
    string District,
    int SelectedDays,
    int RfidWarehouseCount,
    int TotalWarehouseCount,
    decimal RfidSuccessPercentage,
    IReadOnlyList<string> RfidWarehouseIds,
    IReadOnlyList<string> ManualWarehouseIds);

public sealed record PitStopDistrictRow(
    string District,
    IReadOnlyDictionary<string, string> WarehouseStatuses,
    int RfidCount,
    int ManualOnlyCount)
{
    public int Dst => RfidCount + ManualOnlyCount;
    public decimal RfidInstalled => Dst == 0 ? 0 : decimal.Round((decimal)RfidCount / Dst * 100, 2);
}
