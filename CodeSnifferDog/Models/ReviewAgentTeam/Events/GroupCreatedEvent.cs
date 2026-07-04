namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

/// <summary>
/// Event published when one agent group is created.
/// </summary>
internal sealed record GroupCreatedEvent : StatusEvent
{
    /// <summary>
    /// Gets the stable key of the created group.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Gets the display name shown for the created group.
    /// </summary>
    public required string DisplayName { get; init; }
}
