namespace PanoramicAdmin.Models;

public class MasterIssue
{
    public int Id { get; set; }
    public required string Issue { get; set; }
    public bool Active { get; set; } = true;
}

public sealed record MasterIssueInput(string Issue, bool Active);
