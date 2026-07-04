namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Represents a live update that removes timeline entries from an agent view.
/// </summary>
public sealed class TimelineEntriesRemovedDto
{
    /// <summary>
    /// Gets the owning agent identifier.
    /// </summary>
    public required Guid AgentId { get; init; }

    /// <summary>
    /// Gets the identifiers of timeline entries that were removed.
    /// </summary>
    public required IReadOnlyList<Guid> TimelineEntryIds { get; init; }
}
