namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentLiveSubscriptionRequestDto
{
    public required Guid ProjectId { get; init; }

    public required DateTimeOffset SnapshotGeneratedAtUtc { get; init; }

    public required IReadOnlyList<ProjectAgentLiveCursorDto> AgentCursors { get; init; }
}
