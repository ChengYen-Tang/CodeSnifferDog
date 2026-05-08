namespace CodeSnifferDog.Models.ReviewAgentTeam;

internal sealed record AssistantMessageAppendedEvent : AgentStatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }

    public required string Message { get; init; }
}
