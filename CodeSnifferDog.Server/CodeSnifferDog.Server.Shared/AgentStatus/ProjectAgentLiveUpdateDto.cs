namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentLiveUpdateDto
{
    public required Guid ProjectId { get; init; }

    public required ProjectAgentLiveUpdateKind Kind { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public ProjectAgentGroupLiveDto? Group { get; init; }

    public ProjectAgentLiveDto? Agent { get; init; }

    public ProjectAgentStatusChangedDto? AgentStatus { get; init; }

    public ProjectAgentTimelineEntryDto? TimelineEntry { get; init; }
}
