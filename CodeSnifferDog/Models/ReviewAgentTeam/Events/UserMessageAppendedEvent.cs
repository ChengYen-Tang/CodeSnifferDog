namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

/// <summary>
/// Event published when a user message is appended to an agent transcript.
/// </summary>
internal sealed record UserMessageAppendedEvent : StatusEvent
{
    /// <summary>
    /// Gets the stable key of the owning agent group.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Gets the stable key of the agent whose transcript changed.
    /// </summary>
    public required string AgentKey { get; init; }

    /// <summary>
    /// Gets the appended user message text.
    /// </summary>
    public required string Message { get; init; }
}
