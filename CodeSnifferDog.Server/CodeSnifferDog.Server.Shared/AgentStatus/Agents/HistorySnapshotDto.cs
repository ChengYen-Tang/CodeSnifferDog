namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

public sealed class HistorySnapshotDto
{
    public required Guid ProjectId { get; init; }

    public required Guid AgentId { get; init; }

    public required IReadOnlyList<TimelineEntryDto> TimelineEntries { get; init; }
}
