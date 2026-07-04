namespace CodeSnifferDog.Server.Data.Entities;

/// <summary>
/// Persists a timeline entry emitted during agent execution.
/// </summary>
public sealed class ProjectAgentTimelineEntryRecord
{
    /// <summary>
    /// Gets or sets the timeline entry identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning agent identifier.
    /// </summary>
    public Guid ProjectAgentId { get; set; }

    /// <summary>
    /// Gets or sets the per-agent sequence number.
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// Gets or sets the timeline entry type.
    /// </summary>
    public ProjectAgentTimelineEntryType EntryType { get; set; }

    /// <summary>
    /// Gets or sets the message payload for input, output, or compaction entries.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the tool name for tool entries.
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Gets or sets the runtime tool call identifier.
    /// </summary>
    public string? ToolCallId { get; set; }

    /// <summary>
    /// Gets or sets the serialized tool arguments.
    /// </summary>
    public string? ToolArguments { get; set; }

    /// <summary>
    /// Gets or sets the serialized tool result.
    /// </summary>
    public string? ToolResult { get; set; }

    /// <summary>
    /// Gets or sets when the timeline entry occurred.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning agent navigation property.
    /// </summary>
    public ProjectAgentRecord? Agent { get; set; }
}
