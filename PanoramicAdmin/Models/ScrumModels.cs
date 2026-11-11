namespace PanoramicAdmin.Models;

public class ScrumSprint
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Initiative { get; set; } = ScrumInitiative.SmartRefueling;
    public int? ProductBacklogItemId { get; set; }
    public ScrumProductBacklogItem? ProductBacklogItem { get; set; }
    public required string Focus { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public required string Status { get; set; }
    public ICollection<ScrumTask> Tasks { get; set; } = [];
    public ICollection<ScrumBlocker> Blockers { get; set; } = [];
}

public class ScrumTask
{
    public int Id { get; set; }
    public int SprintId { get; set; }
    public ScrumSprint? Sprint { get; set; }
    public required string Title { get; set; }
    public string? Owner { get; set; }
    public string? Category { get; set; }
    public required string Priority { get; set; }
    public required string Status { get; set; }
    public decimal? EstimatedHour { get; set; }
    public string? Notes { get; set; }
}

public class ScrumBlocker
{
    public int Id { get; set; }
    public int SprintId { get; set; }
    public ScrumSprint? Sprint { get; set; }
    public required string Title { get; set; }
    public required string Detail { get; set; }
    public string? Owner { get; set; }
    public required string NextStep { get; set; }
    public bool IsResolved { get; set; }
}

public class ScrumProductBacklogItem
{
    public int Id { get; set; }
    public string Initiative { get; set; } = ScrumInitiative.SmartRefueling;
    public int InitiativeNumber { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string AcceptanceCriteria { get; set; } = string.Empty;
    public required string Priority { get; set; }
    public required string Status { get; set; }
    public int? TargetSprint { get; set; }
    public string DefinitionOfDone { get; set; } = string.Empty;
    public ICollection<ScrumSprint> Sprints { get; set; } = [];
    public ICollection<ScrumProductBacklogItemImage> Images { get; set; } = [];
}

public class ScrumProductBacklogItemImage
{
    public int Id { get; set; }
    public int ProductBacklogItemId { get; set; }
    public ScrumProductBacklogItem? ProductBacklogItem { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
}

public sealed record ScrumSprintInput(
    int? Id,
    int? ProductBacklogItemId,
    string Focus,
    DateTime StartDate,
    DateTime EndDate,
    string Status);

public sealed record ScrumSprintRow(
    int Id,
    string Name,
    int? ProductBacklogItemId,
    string? ProductBacklogItemCode,
    string? ProductBacklogItemSummary,
    string Focus,
    DateTime StartDate,
    DateTime EndDate,
    string Status);

public sealed record ScrumTaskInput(
    int? Id,
    int SprintId,
    string Title,
    string? Owner,
    string? Category,
    string Priority,
    string Status,
    decimal? EstimatedHour,
    string? Notes,
    ScrumBlockerInput? Blocker = null);

public sealed record ScrumTaskRow(
    int Id,
    string Code,
    int SprintId,
    string Title,
    string Owner,
    string Initials,
    string? Category,
    string Priority,
    string Status,
    decimal? EstimatedHour,
    string? Notes);

public sealed record ScrumBlockerInput(
    int? Id,
    int SprintId,
    string Title,
    string Detail,
    string? Owner,
    string NextStep,
    bool IsResolved);

public sealed record ScrumBlockerRow(
    int Id,
    int SprintId,
    string Title,
    string Detail,
    string Owner,
    string NextStep,
    bool IsResolved);

public sealed record ScrumProductBacklogItemInput(
    int? Id,
    string Module,
    string Summary,
    string AcceptanceCriteria,
    string Priority,
    int? TargetSprint,
    string DefinitionOfDone,
    IReadOnlyList<ScrumProductBacklogImageUpload>? Images = null);

public sealed record ScrumProductBacklogImageUpload(byte[] Content, string ContentType, string Extension);

public sealed record ScrumProductBacklogItemImageRow(int Id, string Url);

public sealed record ScrumProductBacklogItemRow(
    int Id,
    string Code,
    string Module,
    string Summary,
    string AcceptanceCriteria,
    string Priority,
    int? TargetSprint,
    string DefinitionOfDone,
    double? SprintCompletionPercent,
    IReadOnlyList<ScrumProductBacklogItemImageRow> Images);

public sealed record ScrumProductBacklogItemOption(
    int Id,
    string Code,
    string Summary,
    int? TargetSprint);

public sealed record ScrumPicFilterOption(string Label, string? Owner);

public static class ScrumProductBacklogCatalog
{
    public static IReadOnlyList<string> Priorities { get; } =
        ["Rendah", "Sedang", "Tinggi"];
}

public static class ScrumInitiative
{
    public const string SmartRefueling = "SMARE";
    public const string Omp = "OMP";

    public static bool IsSupported(string? initiative) =>
        initiative is SmartRefueling or Omp;

    public static string GetProductBacklogItemCode(string initiative, int id) =>
        $"PBI-{initiative}-{id:000}";

    public static string GetTaskCode(string initiative, int number) =>
        $"TSK-{initiative}-{number:000}";
}
