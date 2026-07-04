namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Represents a single timeline entry emitted by an agent.
/// </summary>
public sealed class TimelineEntryDto
{
    /// <summary>
    /// Gets the timeline entry identifier.
    /// </summary>
    public required Guid TimelineEntryId { get; init; }

    /// <summary>
    /// Gets the owning agent identifier.
    /// </summary>
    public required Guid AgentId { get; init; }

    /// <summary>
    /// Gets the per-agent sequence number.
    /// </summary>
    public required long Sequence { get; init; }

    /// <summary>
    /// Gets the timeline entry kind.
    /// </summary>
    public required TimelineEntryKind EntryKind { get; init; }

    /// <summary>
    /// Gets when the timeline entry occurred.
    /// </summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// Gets the message payload for input, output, or compaction entries.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets the runtime tool call identifier.
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// Gets the tool name for tool entries.
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// Gets the serialized tool arguments.
    /// </summary>
    public string? ToolArguments { get; init; }

    /// <summary>
    /// Gets the serialized tool result.
    /// </summary>
    public string? ToolResult { get; init; }
}
