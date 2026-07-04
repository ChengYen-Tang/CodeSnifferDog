namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Carries the loaded timeline history for a single agent.
/// </summary>
public sealed class HistorySnapshotDto
{
    /// <summary>
    /// Gets the project identifier.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the agent identifier.
    /// </summary>
    public required Guid AgentId { get; init; }

    /// <summary>
    /// Gets the loaded timeline entries.
    /// </summary>
    public required IReadOnlyList<TimelineEntryDto> TimelineEntries { get; init; }
}
