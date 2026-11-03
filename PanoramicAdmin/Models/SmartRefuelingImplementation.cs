namespace PanoramicAdmin.Models;

public sealed class SmartRefuelingImplementation
{
    public int Id { get; set; }
    public required string Site { get; set; }
    public required string Activity { get; set; }
    public string? Status2026Q1 { get; set; }
    public string? Status2026Q2 { get; set; }
    public string? Status2026Q3 { get; set; }
    public string? Status2026Q4 { get; set; }
    public string? Status2027Q1 { get; set; }
    public string? Status2027Q2 { get; set; }
    public string? Status2027Q3 { get; set; }
    public string? Status2027Q4 { get; set; }
}

public static class SmartRefuelingImplementationCatalog
{
    public static IReadOnlyList<string> Periods { get; } =
    [
        "2026 Q1",
        "2026 Q2",
        "2026 Q3",
        "2026 Q4",
        "2027 Q1",
        "2027 Q2",
        "2027 Q3",
        "2027 Q4"
    ];

    public static IReadOnlyList<string> Statuses { get; } =
    [
        "Done",
        "Progress",
        "Overdue",
        "Plan",
        "Unplanned"
    ];
}
