namespace CodeSnifferDog.Models.ReviewAgentTeam;

internal sealed record AgentCompactionEvent : AgentStatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }
}
