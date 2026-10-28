namespace PanoramicAdmin.Models;

public class RfidTagManualUnit
{
    public int Id { get; set; }
    public required string District { get; set; }
    public required string EquipClass { get; set; }
    public required string Egi { get; set; }
    public int AdditionalUnitCount { get; set; }
}

public readonly record struct RfidManualUnitKey
{
    public RfidManualUnitKey(string district, string equipClass, string egi)
    {
        District = district.Trim().ToUpperInvariant();
        EquipClass = equipClass.Trim().ToUpperInvariant();
        Egi = egi.Trim().ToUpperInvariant();
    }

    public string District { get; }
    public string EquipClass { get; }
    public string Egi { get; }
}
