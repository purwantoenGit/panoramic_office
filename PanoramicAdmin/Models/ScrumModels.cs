namespace PanoramicAdmin.Models;

public class ScrumSprint
{
    public int Id { get; set; }
    public required string Name { get; set; }
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
    public required string Priority { get; set; }
    public required string Status { get; set; }
    public DateTime? DueDate { get; set; }
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

public sealed record ScrumSprintInput(
    int? Id,
    string Name,
    string Focus,
    DateTime StartDate,
    DateTime EndDate,
    string Status);

public sealed record ScrumSprintRow(
    int Id,
    string Name,
    string Focus,
    DateTime StartDate,
    DateTime EndDate,
    string Status);

public sealed record ScrumTaskInput(
    int? Id,
    int SprintId,
    string Title,
    string? Owner,
    string Priority,
    string Status,
    DateTime? DueDate);

public sealed record ScrumTaskRow(
    int Id,
    string Code,
    int SprintId,
    string Title,
    string Owner,
    string Initials,
    string Priority,
    string Status,
    DateTime? DueDate);

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
