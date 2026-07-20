namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Carries the client's current subscription state for live agent updates.
/// </summary>
public sealed class LiveSubscriptionRequestDto
{
    /// <summary>
    /// Gets the project identifier being subscribed to.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the snapshot timestamp the client currently holds.
    /// </summary>
    public required DateTimeOffset SnapshotGeneratedAtUtc { get; init; }

    /// <summary>
    /// Gets the selected agent identifier, if the client has one.
    /// </summary>
    public Guid? AgentId { get; init; }

    /// <summary>
    /// Gets the latest timeline sequence the client has already received.
    /// </summary>
    public long LatestSequence { get; init; }

    /// <summary>
    /// Gets whether the catch-up response must include current project, group, and agent state.
    /// Same-project agent switches can omit this data because the project channel remains subscribed.
    /// </summary>
    public bool IncludeProjectState { get; init; } = true;
}
