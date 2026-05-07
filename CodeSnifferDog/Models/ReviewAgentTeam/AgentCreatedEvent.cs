namespace CodeSnifferDog.Models.ReviewAgentTeam;

internal sealed record AgentCreatedEvent : AgentStatusEvent
{
    public required string AgentKey { get; init; }

    public required string GroupKey { get; init; }

    public required string DisplayName { get; init; }

    public required string InitialStatus { get; init; }
}
