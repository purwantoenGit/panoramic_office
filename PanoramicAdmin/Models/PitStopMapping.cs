namespace PanoramicAdmin.Models;

public sealed record PitStopWarehouseStatus(
    string District,
    string WarehouseId,
    bool HasRfid,
    bool HasNonRfid);

public sealed record PitStopDistrictRow(
    string District,
    IReadOnlyDictionary<string, string> WarehouseStatuses,
    int RfidCount,
    int ManualOnlyCount)
{
    public int Dst => RfidCount + ManualOnlyCount;
    public decimal RfidInstalled => Dst == 0 ? 0 : decimal.Round((decimal)RfidCount / Dst * 100, 2);
}
