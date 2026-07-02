namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

internal abstract record StatusEvent
{
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
