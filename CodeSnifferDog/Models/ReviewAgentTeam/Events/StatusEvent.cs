namespace CodeSnifferDog.Models.ReviewAgentTeam.Events;

/// <summary>
/// Base type for timestamped review-agent status events.
/// </summary>
internal abstract record StatusEvent
{
    /// <summary>
    /// Gets when the event occurred, in UTC.
    /// </summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
