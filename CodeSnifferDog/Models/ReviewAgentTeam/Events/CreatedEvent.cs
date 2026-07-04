namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

/// <summary>
/// Event published when one agent is created.
/// </summary>
internal sealed record CreatedEvent : StatusEvent
{
    /// <summary>
    /// Gets the stable key of the created agent.
    /// </summary>
    public required string AgentKey { get; init; }

    /// <summary>
    /// Gets the stable key of the owning agent group.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Gets the display name shown for the created agent.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the system prompt assigned to the created agent.
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Gets the initial status assigned to the created agent.
    /// </summary>
    public required string InitialStatus { get; init; }
}
