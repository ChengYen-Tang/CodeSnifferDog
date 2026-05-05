namespace CodeSnifferDog.Server.Data.Entities;

public sealed class ProjectAgentRecord
{
    public Guid Id { get; set; }

    public Guid ProjectAgentGroupId { get; set; }

    public required string RuntimeKey { get; set; }

    public required string DisplayName { get; set; }

    public ProjectAgentStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ProjectAgentGroupRecord? Group { get; set; }

    public List<ProjectAgentTimelineEntryRecord> TimelineEntries { get; set; } = [];
}
