namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

internal sealed record ToolCallCompletedEvent : StatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }

    public required string ToolCallId { get; init; }

    public string? Result { get; init; }
}
