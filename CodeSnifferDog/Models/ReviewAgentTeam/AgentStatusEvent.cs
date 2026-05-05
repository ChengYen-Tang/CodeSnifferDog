namespace CodeSnifferDog.Models.ReviewAgentTeam;

public abstract record AgentStatusEvent
{
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
