namespace CodeSnifferDog.Models.ReviewAgentTeam;

internal sealed record UserMessageAppendedEvent : AgentStatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }

    public required string Message { get; init; }
}
