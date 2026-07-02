namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

internal sealed record CreatedEvent : StatusEvent
{
    public required string AgentKey { get; init; }

    public required string GroupKey { get; init; }

    public required string DisplayName { get; init; }

    public required string SystemPrompt { get; init; }

    public required string InitialStatus { get; init; }
}
