namespace PanoramicAdmin.Models;

public sealed class SopEvaluation
{
    public int Id { get; set; }
    public required string ApplicationName { get; set; }
    public required string SopCategory { get; set; }
    public int? Level { get; set; }
    public string? DocumentTitle { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Revision { get; set; }
    public string? OwnerName { get; set; }
    public string? EvidenceUrl { get; set; }
    public DateTime? EvaluationDate { get; set; }
    public string? Findings { get; set; }
    public string? FollowUpAction { get; set; }
    public DateTime? FollowUpDueDate { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed record SopEvaluationCategory(string Name, string Focus, string Priority);

public static class SopEvaluationCatalog
{
    public static IReadOnlyList<SopEvaluationCategory> Categories { get; } =
    [
        new("Operational Usage",
            "Bagaimana produk digunakan dalam aktivitas operasional rutin",
            "Critical"),
        new("Monitoring & Follow-up",
            "Bagaimana informasi, alarm, dan deviasi ditindaklanjuti hingga selesai",
            "Critical"),
        new("Exception & Escalation",
            "Bagaimana menangani kondisi abnormal, kegagalan sistem, dan eskalasi",
            "Critical"),
        new("Technical Support & Recovery",
            "Bagaimana menjaga keandalan dan memulihkan fungsi produk",
            "High"),
        new("Performance & Compliance Evaluation",
            "Bagaimana mengukur kepatuhan, efektivitas penggunaan, dan hasil operasional",
            "High")
    ];

    public static IReadOnlyList<string> ApplicationNames { get; } =
    [
        "OPA",
        "GPS TRACKING",
        "IFLASH",
        "OMP",
        "CAS",
        "OBSMS",
        "MH02",
        "H2D",
        "MIR",
        "REMOTE DOZER"
    ];

    public static string GetLevelName(int? level) => level switch
    {
        0 => "Not Available",
        1 => "Drafted",
        2 => "Approved",
        3 => "Implemented",
        4 => "Verified",
        _ => "Belum dinilai"
    };

    public static string GetLevelDescription(int level) => level switch
    {
        0 => "SOP belum tersedia.",
        1 => "SOP telah disusun tetapi belum disahkan.",
        2 => "SOP telah disahkan.",
        3 => "SOP telah diterapkan dan pengguna telah mendapatkan sosialisasi atau pelatihan.",
        4 => "Implementasi telah diverifikasi melalui bukti kepatuhan dan evaluasi efektivitas.",
        _ => string.Empty
    };
}
