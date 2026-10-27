using MudBlazor;

namespace PanoramicAdmin.Models;

public sealed record ProjectLogoOption(string Key, string Label, string Icon);

public static class ProjectLogoCatalog
{
    public const string DefaultKey = "folder";

    public static IReadOnlyList<ProjectLogoOption> Options { get; } =
        new ProjectLogoOption[]
        {
        new(DefaultKey, "Folder", Icons.Material.Filled.Folder),
        new("dashboard", "Dashboard", Icons.Material.Filled.Dashboard),
        new("widgets", "Aplikasi", Icons.Material.Filled.Widgets),
        new("business", "Bisnis", Icons.Material.Filled.Business),
        new("event-note", "Agenda", Icons.Material.Filled.EventNote),
        new("clock", "Jam", Icons.Material.Filled.AccessTime),
        new("bug-report", "Issue", Icons.Material.Filled.BugReport),
        new("trending-up", "Strategi", Icons.Material.Filled.TrendingUp),
        new("groups", "Tim", Icons.Material.Filled.Groups),
        new("stream", "Stream", Icons.Material.Filled.Stream),
        new("label", "Kategori", Icons.Material.Filled.Label),
        new("place", "Lokasi", Icons.Material.Filled.Place),
        new("people", "PIC", Icons.Material.Filled.People),
        new("account-tree", "Struktur", Icons.Material.Filled.AccountTree),
        new("analytics", "Analitik", Icons.Material.Filled.Analytics),
        new("apps", "Aplikasi", Icons.Material.Filled.Apps),
        new("assessment", "Laporan", Icons.Material.Filled.Assessment),
        new("build", "Peralatan", Icons.Material.Filled.Build),
        new("business-center", "Kantor", Icons.Material.Filled.BusinessCenter),
        new("camera", "Kamera", Icons.Material.Filled.CameraAlt),
        new("campaign", "Komunikasi", Icons.Material.Filled.Campaign),
        new("cloud", "Cloud", Icons.Material.Filled.Cloud),
        new("code", "Pengembangan", Icons.Material.Filled.Code),
        new("computer", "Komputer", Icons.Material.Filled.Computer),
        new("construction", "Konstruksi", Icons.Material.Filled.Construction),
        new("devices", "Perangkat", Icons.Material.Filled.Devices),
        new("domain", "Organisasi", Icons.Material.Filled.Domain),
        new("inventory", "Inventaris", Icons.Material.Filled.Inventory2),
        new("fuel", "Bahan Bakar", Icons.Material.Filled.LocalGasStation),
        new("map", "Peta", Icons.Material.Filled.Map),
        new("manufacturing", "Manufaktur", Icons.Material.Filled.PrecisionManufacturing),
        new("security", "Keamanan", Icons.Material.Filled.Security),
        new("settings", "Pengaturan", Icons.Material.Filled.Settings),
        new("shield", "Perlindungan", Icons.Material.Filled.Shield),
        new("storage", "Penyimpanan", Icons.Material.Filled.Storage),
        new("task", "Tugas", Icons.Material.Filled.TaskAlt),
        new("warehouse", "Gudang", Icons.Material.Filled.Warehouse),
        new("web", "Web", Icons.Material.Filled.Web),
        new("workspaces", "Workspace", Icons.Material.Filled.Workspaces),
        new("science", "Sains", Icons.Material.Filled.Science),
        new("speed", "Performa", Icons.Material.Filled.Speed)
        }
        .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(option => option.Key, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static bool IsValidKey(string? key) =>
        Options.Any(option => string.Equals(option.Key, key, StringComparison.Ordinal));

    public static string GetIcon(string? key) =>
        Options.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.Ordinal))?.Icon
        ?? Icons.Material.Filled.HelpOutline;
}
