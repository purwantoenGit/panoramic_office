namespace PanoramicAdmin.Models;

public class ProjectIssue
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public required string StatusProject { get; set; }
    public DateTime Tanggal { get; set; }
    public int IssuerPicId { get; set; }
    public MasterPic? IssuerPic { get; set; }
    public int StrategicActionId { get; set; }
    public StrategicAction? StrategicAction { get; set; }
    public DateTime? DueDate { get; set; }
    public ICollection<ProjectIssueActionPlan> ActionPlans { get; set; } = [];
}
