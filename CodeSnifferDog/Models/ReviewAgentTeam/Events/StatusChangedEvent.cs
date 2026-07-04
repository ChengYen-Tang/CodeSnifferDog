namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

/// <summary>
/// Event published when one agent changes status.
/// </summary>
internal sealed record StatusChangedEvent : StatusEvent
{
    /// <summary>
    /// Gets the stable key of the owning agent group.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Gets the stable key of the agent whose status changed.
    /// </summary>
    public required string AgentKey { get; init; }

    /// <summary>
    /// Gets the new status value.
    /// </summary>
    public required string Status { get; init; }
}
