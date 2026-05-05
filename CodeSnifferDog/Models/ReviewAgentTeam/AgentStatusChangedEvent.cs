namespace CodeSnifferDog.Models.ReviewAgentTeam;

public sealed record AgentStatusChangedEvent : AgentStatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }

    public required string Status { get; init; }
}
