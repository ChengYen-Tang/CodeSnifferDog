namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

public sealed class TimelineEntriesRemovedDto
{
    public required Guid AgentId { get; init; }

    public required IReadOnlyList<Guid> TimelineEntryIds { get; init; }
}
