namespace CodeSnifferDog.Models.ReviewAgentTeam;

internal sealed record AgentTranscriptClearedEvent : AgentStatusEvent
{
    public required string GroupKey { get; init; }

    public required string AgentKey { get; init; }

    public required DateTimeOffset ClearAfterUtc { get; init; }
}
