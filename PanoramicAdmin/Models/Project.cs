namespace PanoramicAdmin.Models;

public class Project
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Stream { get; set; }
    public required string CategoryProject { get; set; }
    public List<ProjectSite> Sites { get; set; } = [];
}

public class ProjectSite
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public required string Name { get; set; }
}
