namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

internal sealed record GroupCreatedEvent : StatusEvent
{
    public required string GroupKey { get; init; }

    public required string DisplayName { get; init; }
}
