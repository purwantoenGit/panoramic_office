namespace PanoramicAdmin.Models;

public class ProjectIssueActionPlan
{
    public int Id { get; set; }
    public int ProjectIssueId { get; set; }
    public ProjectIssue? ProjectIssue { get; set; }
    public required string ActionPlan { get; set; }
    public required string Status { get; set; }
    public int PicId { get; set; }
    public MasterPic? Pic { get; set; }
    public ICollection<ProjectIssueActionPlanPic> PicAssignments { get; set; } = [];
    public string? Feedback { get; set; }
}
