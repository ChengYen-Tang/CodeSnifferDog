using ExecutionStatusChangedDto = CodeSnifferDog.Server.Shared.AgentStatus.Execution.StatusChangedDto;

namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Represents a live update pushed to agent-status clients.
/// </summary>
public sealed class LiveUpdateDto
{
    /// <summary>
    /// Gets the project identifier that owns the update.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the live update kind.
    /// </summary>
    public required LiveUpdateKind Kind { get; init; }

    /// <summary>
    /// Gets when the update occurred.
    /// </summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// Gets the group payload when the update concerns an agent group.
    /// </summary>
    public GroupLiveDto? Group { get; init; }

    /// <summary>
    /// Gets the agent payload when the update concerns an agent insert or update.
    /// </summary>
    public LiveDto? Agent { get; init; }

    /// <summary>
    /// Gets the agent-status payload when the update concerns a run-status change.
    /// </summary>
    public StatusChangedDto? AgentStatus { get; init; }

    /// <summary>
    /// Gets the timeline-entry payload when the update concerns a timeline mutation.
    /// </summary>
    public TimelineEntryDto? TimelineEntry { get; init; }

    /// <summary>
    /// Gets the removed-timeline payload when the update removes entries.
    /// </summary>
    public TimelineEntriesRemovedDto? RemovedTimelineEntries { get; init; }

    /// <summary>
    /// Gets the project-status payload when the update concerns project execution state.
    /// </summary>
    public ExecutionStatusChangedDto? ProjectStatus { get; init; }
}
