using Microsoft.EntityFrameworkCore;
using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectSite> ProjectSites => Set<ProjectSite>();
    public DbSet<MasterStream> MasterStreams => Set<MasterStream>();
    public DbSet<MasterCategoryProject> MasterCategoryProjects => Set<MasterCategoryProject>();
    public DbSet<MasterSite> MasterSites => Set<MasterSite>();
    public DbSet<MasterPic> MasterPics => Set<MasterPic>();
    public DbSet<MasterRole> MasterRoles => Set<MasterRole>();
    public DbSet<MasterIssue> MasterIssues => Set<MasterIssue>();
    public DbSet<StrategicAction> StrategicActions => Set<StrategicAction>();
    public DbSet<ProjectIssue> ProjectIssues => Set<ProjectIssue>();
    public DbSet<ProjectIssueActionPlan> ProjectIssueActionPlans => Set<ProjectIssueActionPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MasterCategoryProject>(entity =>
        {
            entity.ToTable("pano_master_category_project", "dbo");
            entity.Property(category => category.CategoryProject)
                .HasColumnName("CategoryProject")
                .HasMaxLength(50)
                .IsRequired();
        });

        modelBuilder.Entity<MasterStream>(entity =>
        {
            entity.ToTable("pano_master_stream", "dbo");
            entity.Property(stream => stream.NamaStream)
                .HasColumnName("NamaStream")
                .HasMaxLength(100)
                .IsRequired();
        });

        modelBuilder.Entity<MasterSite>(entity =>
        {
            entity.ToTable("pano_master_sites", "dbo", table =>
                table.HasCheckConstraint(
                    "CK_pano_master_sites_Status",
                    "[Status] COLLATE Latin1_General_100_BIN2 IN (N'Active', N'Non Active')"));
            entity.Property(site => site.Sites)
                .HasColumnName("Sites")
                .HasMaxLength(150)
                .IsRequired();
            entity.Property(site => site.Status)
                .HasColumnName("Status")
                .HasMaxLength(20)
                .IsRequired();
        });

        modelBuilder.Entity<MasterPic>(entity =>
        {
            entity.ToTable("pano_master_pic", "dbo");
            entity.Property(pic => pic.Nama)
                .HasColumnName("Nama")
                .HasMaxLength(150)
                .IsRequired();
            entity.Property(pic => pic.Role)
                .HasColumnName("Role")
                .HasMaxLength(100)
                .IsRequired();
        });

        modelBuilder.Entity<MasterRole>(entity =>
        {
            entity.ToTable("pano_master_roles", "dbo");
            entity.Property(role => role.Roles)
                .HasColumnName("Roles")
                .HasMaxLength(100)
                .IsRequired();
            entity.HasIndex(role => role.Roles)
                .IsUnique()
                .HasDatabaseName("UX_pano_master_roles_Roles");
        });

        modelBuilder.Entity<MasterIssue>(entity =>
        {
            entity.ToTable("pano_master_issue", "dbo");
            entity.Property(issue => issue.Issue)
                .HasColumnName("Issue")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(issue => issue.Active)
                .HasColumnName("Active")
                .IsRequired();
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("pano_MasterProject", "dbo");
            entity.Property(p => p.Name).HasMaxLength(200);
            entity.Property(p => p.Stream).HasMaxLength(100);
            entity.Property(p => p.CategoryProject)
                .HasColumnName("Status")
                .HasMaxLength(50);
        });

        modelBuilder.Entity<ProjectSite>(entity =>
        {
            entity.ToTable("pano_MasterProjectSite", "dbo");
            entity.Property(s => s.Name).HasMaxLength(100);
            entity.HasOne(s => s.Project)
                .WithMany(p => p.Sites)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StrategicAction>(entity =>
        {
            entity.ToTable("pano_strategic_action", "dbo");
            entity.Property(action => action.NamaStrategicAction)
                .HasColumnName("NamaStrategicAction")
                .HasMaxLength(200)
                .IsRequired();
        });

        modelBuilder.Entity<ProjectIssue>(entity =>
        {
            entity.ToTable("pano_project_issue", "dbo");
            entity.Property(issue => issue.StatusProject)
                .HasColumnName("StatusProject")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(issue => issue.Tanggal)
                .HasColumnName("Tanggal")
                .HasColumnType("date")
                .IsRequired();
            entity.Property(issue => issue.DueDate)
                .HasColumnName("DueDate")
                .HasColumnType("date");
            entity.HasOne(issue => issue.Project)
                .WithMany()
                .HasForeignKey(issue => issue.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(issue => issue.StrategicAction)
                .WithMany()
                .HasForeignKey(issue => issue.StrategicActionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(issue => issue.IssuerPic)
                .WithMany()
                .HasForeignKey(issue => issue.IssuerPicId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectIssueActionPlan>(entity =>
        {
            entity.ToTable("pano_project_issue_action_plan", "dbo");
            entity.Property(plan => plan.ActionPlan)
                .HasColumnName("ActionPlan")
                .HasColumnType("nvarchar(max)")
                .IsRequired();
            entity.Property(plan => plan.Status)
                .HasColumnName("Status")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(plan => plan.Feedback)
                .HasColumnName("Feedback")
                .HasColumnType("nvarchar(max)");
            entity.HasOne(plan => plan.ProjectIssue)
                .WithMany(issue => issue.ActionPlans)
                .HasForeignKey(plan => plan.ProjectIssueId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(plan => plan.Pic)
                .WithMany()
                .HasForeignKey(plan => plan.PicId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
