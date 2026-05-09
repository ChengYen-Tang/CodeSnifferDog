namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentTimelineEntryDto
{
    public required Guid TimelineEntryId { get; init; }

    public required Guid AgentId { get; init; }

    public required long Sequence { get; init; }

    public required ProjectAgentTimelineEntryKind EntryKind { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public string? Message { get; init; }

    public string? ToolCallId { get; init; }

    public string? ToolName { get; init; }

    public string? ToolArguments { get; init; }

    public string? ToolResult { get; init; }
}
