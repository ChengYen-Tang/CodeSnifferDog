using ExecutionStatusChangedDto = CodeSnifferDog.Server.Shared.AgentStatus.Execution.StatusChangedDto;

namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

public sealed class LiveUpdateDto
{
    public required Guid ProjectId { get; init; }

    public required LiveUpdateKind Kind { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public GroupLiveDto? Group { get; init; }

    public LiveDto? Agent { get; init; }

    public StatusChangedDto? AgentStatus { get; init; }

    public TimelineEntryDto? TimelineEntry { get; init; }

    public TimelineEntriesRemovedDto? RemovedTimelineEntries { get; init; }

    public ExecutionStatusChangedDto? ProjectStatus { get; init; }
}
