namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

/// <summary>
/// Event published when one agent starts a tool call.
/// </summary>
internal sealed record ToolCallStartedEvent : StatusEvent
{
    /// <summary>
    /// Gets the stable key of the owning agent group.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Gets the stable key of the agent that started the tool call.
    /// </summary>
    public required string AgentKey { get; init; }

    /// <summary>
    /// Gets the stable identifier of the started tool call.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// Gets the invoked tool name.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the serialized tool arguments, when available.
    /// </summary>
    public string? Arguments { get; init; }
}
