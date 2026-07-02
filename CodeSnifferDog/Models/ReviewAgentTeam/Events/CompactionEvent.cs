namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

internal sealed record CompactionEvent : StatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }
}
