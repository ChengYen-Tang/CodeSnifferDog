namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

/// <summary>
/// Event published when transcript messages older than a cutoff were cleared.
/// </summary>
internal sealed record TranscriptClearedEvent : StatusEvent
{
    /// <summary>
    /// Gets the stable key of the owning agent group.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Gets the stable key of the agent whose transcript was cleared.
    /// </summary>
    public required string AgentKey { get; init; }

    /// <summary>
    /// Gets the newest timestamp guaranteed to have been cleared.
    /// </summary>
    public required DateTimeOffset ClearAfterUtc { get; init; }
}
