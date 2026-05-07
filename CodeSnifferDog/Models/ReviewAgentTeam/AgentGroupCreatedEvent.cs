namespace CodeSnifferDog.Models.ReviewAgentTeam;

internal sealed record AgentGroupCreatedEvent : AgentStatusEvent
{
    public required string GroupKey { get; init; }

    public required string DisplayName { get; init; }
}
