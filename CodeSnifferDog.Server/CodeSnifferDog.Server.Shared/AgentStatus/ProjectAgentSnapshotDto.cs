namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentSnapshotDto
{
    public required Guid AgentId { get; init; }

    public required Guid GroupId { get; init; }

    public required string RuntimeKey { get; init; }

    public required string DisplayName { get; init; }

    public required ProjectAgentRunStatus Status { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required bool HasLoadedHistory { get; init; }

    public required IReadOnlyList<ProjectAgentTimelineEntryDto> TimelineEntries { get; init; }
}
