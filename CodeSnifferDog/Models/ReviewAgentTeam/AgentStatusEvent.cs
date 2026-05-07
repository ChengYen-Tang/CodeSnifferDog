namespace CodeSnifferDog.Models.ReviewAgentTeam;

internal abstract record AgentStatusEvent
{
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
