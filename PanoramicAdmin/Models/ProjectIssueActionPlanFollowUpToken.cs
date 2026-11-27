namespace PanoramicAdmin.Models;

public sealed class ProjectIssueActionPlanFollowUpToken
{
    public int Id { get; set; }
    public required string TokenHash { get; set; }
    public int ActionPlanId { get; set; }
    public ProjectIssueActionPlan? ActionPlan { get; set; }
    public int PicId { get; set; }
    public MasterPic? Pic { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}
