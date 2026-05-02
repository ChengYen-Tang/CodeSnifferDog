namespace CodeSnifferDog.Server.Data.Entities;

public sealed class ProjectAgentTimelineEntryRecord
{
    public Guid Id { get; set; }

    public Guid ProjectAgentId { get; set; }

    public long Sequence { get; set; }

    public ProjectAgentTimelineEntryType EntryType { get; set; }

    public string? Message { get; set; }

    public string? ToolName { get; set; }

    public string? ToolArguments { get; set; }

    public string? ToolResult { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public ProjectAgentRecord? Agent { get; set; }
}
