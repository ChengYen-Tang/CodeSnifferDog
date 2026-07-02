namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

internal sealed record AssistantMessageAppendedEvent : StatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }

    public required string Message { get; init; }
}
