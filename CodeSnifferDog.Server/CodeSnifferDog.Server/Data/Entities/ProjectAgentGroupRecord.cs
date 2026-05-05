namespace CodeSnifferDog.Server.Data.Entities;

public sealed class ProjectAgentGroupRecord
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public required string RuntimeKey { get; set; }

    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ProjectRecord? Project { get; set; }

    public List<ProjectAgentRecord> Agents { get; set; } = [];
}
