namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentHistorySnapshotDto
{
    public required Guid ProjectId { get; init; }

    public required Guid AgentId { get; init; }

    public required IReadOnlyList<ProjectAgentTimelineEntryDto> TimelineEntries { get; init; }
}
