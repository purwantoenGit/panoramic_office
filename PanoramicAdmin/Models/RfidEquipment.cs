namespace PanoramicAdmin.Models;

public sealed record RfidEquipmentGroup(
    string District,
    string EquipClass,
    string Egi,
    int UnitCount);
