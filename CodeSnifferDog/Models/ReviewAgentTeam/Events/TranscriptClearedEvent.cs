namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

internal sealed record TranscriptClearedEvent : StatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }

    public required DateTimeOffset ClearAfterUtc { get; init; }
}
