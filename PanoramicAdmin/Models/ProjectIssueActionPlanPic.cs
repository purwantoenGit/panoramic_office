namespace PanoramicAdmin.Models;

public class ProjectIssueActionPlanPic
{
    public int ProjectIssueActionPlanId { get; set; }
    public ProjectIssueActionPlan? ActionPlan { get; set; }
    public int PicId { get; set; }
    public MasterPic? Pic { get; set; }
}
