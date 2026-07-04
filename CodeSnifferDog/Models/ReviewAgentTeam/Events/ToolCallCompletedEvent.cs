namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

/// <summary>
/// Event published when one agent tool call completes.
/// </summary>
internal sealed record ToolCallCompletedEvent : StatusEvent
{
    /// <summary>
    /// Gets the stable key of the owning agent group.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Gets the stable key of the agent that completed the tool call.
    /// </summary>
    public required string AgentKey { get; init; }

    /// <summary>
    /// Gets the stable identifier of the completed tool call.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// Gets the serialized tool result, when one was captured.
    /// </summary>
    public string? Result { get; init; }
}
