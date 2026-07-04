namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

/// <summary>
/// Event published when an agent compacts its transcript context.
/// </summary>
internal sealed record CompactionEvent : StatusEvent
{
    /// <summary>
    /// Gets the stable key of the owning agent group.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Gets the stable key of the agent that compacted its context.
    /// </summary>
    public required string AgentKey { get; init; }
}
