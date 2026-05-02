using CodeSnifferDog.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Data;

public sealed class CodeSnifferDogServerDbContext(DbContextOptions<CodeSnifferDogServerDbContext> options) : DbContext(options)
{
    public DbSet<ProjectRecord> Projects => Set<ProjectRecord>();

    public DbSet<ProjectRuleReportRecord> ProjectRuleReports => Set<ProjectRuleReportRecord>();

    public DbSet<ProjectAgentGroupRecord> ProjectAgentGroups => Set<ProjectAgentGroupRecord>();

    public DbSet<ProjectAgentRecord> ProjectAgents => Set<ProjectAgentRecord>();

    public DbSet<ProjectAgentTimelineEntryRecord> ProjectAgentTimelineEntries => Set<ProjectAgentTimelineEntryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectRecord>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(project => project.Id);

            entity.HasIndex(project => new
            {
                project.Status,
                project.QueueTimestampUtc,
                project.CreatedAtUtc,
            });

            entity.HasMany(project => project.RuleReports)
                .WithOne(report => report.Project)
                .HasForeignKey(report => report.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(project => project.AgentGroups)
                .WithOne(group => group.Project)
                .HasForeignKey(group => group.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectRuleReportRecord>(entity =>
        {
            entity.ToTable("ProjectRuleReports");
            entity.HasKey(report => report.Id);

            entity.Property(report => report.RuleKeyHash)
                .HasMaxLength(64);

            entity.HasIndex(issue => new
            {
                issue.ProjectId,
                issue.RuleKeyHash,
            }).IsUnique();
        });

        modelBuilder.Entity<ProjectAgentGroupRecord>(entity =>
        {
            entity.ToTable("ProjectAgentGroups");
            entity.HasKey(group => group.Id);

            entity.HasIndex(group => new
            {
                group.ProjectId,
                group.CreatedAtUtc,
            });

            entity.HasMany(group => group.Agents)
                .WithOne(agent => agent.Group)
                .HasForeignKey(agent => agent.ProjectAgentGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectAgentRecord>(entity =>
        {
            entity.ToTable("ProjectAgents");
            entity.HasKey(agent => agent.Id);

            entity.HasIndex(agent => new
            {
                agent.ProjectAgentGroupId,
                agent.CreatedAtUtc,
            });

            entity.HasMany(agent => agent.TimelineEntries)
                .WithOne(entry => entry.Agent)
                .HasForeignKey(entry => entry.ProjectAgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectAgentTimelineEntryRecord>(entity =>
        {
            entity.ToTable("ProjectAgentTimelineEntries");
            entity.HasKey(entry => entry.Id);

            entity.HasIndex(entry => new
            {
                entry.ProjectAgentId,
                entry.Sequence,
            }).IsUnique();

            entity.HasIndex(entry => new
            {
                entry.ProjectAgentId,
                entry.OccurredAtUtc,
            });
        });
    }
}
