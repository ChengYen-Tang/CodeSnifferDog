namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentTimelineEntriesRemovedDto
{
    public required Guid AgentId { get; init; }

    public required IReadOnlyList<Guid> TimelineEntryIds { get; init; }
}
