namespace CodeSnifferDog.Models.ReviewAgentTeam;

internal sealed record ToolCallCompletedEvent : AgentStatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }

    public required string ToolCallId { get; init; }

    public string? Result { get; init; }
}
