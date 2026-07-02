namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

internal sealed record ToolCallStartedEvent : StatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }

    public required string ToolCallId { get; init; }

    public required string ToolName { get; init; }

    public string? Arguments { get; init; }
}
