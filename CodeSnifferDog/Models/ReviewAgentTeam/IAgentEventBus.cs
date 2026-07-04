namespace CodeSnifferDog.Models.ReviewAgentTeam;

/// <summary>
/// Creates per-agent event scopes and publishes group-level review-agent events.
/// </summary>
public interface IAgentEventBus
{
    /// <summary>
    /// Creates an event scope bound to one agent inside one agent group.
    /// </summary>
    /// <param name="groupKey">Stable key of the agent group that owns the agent.</param>
    /// <param name="agentKey">Stable key of the agent inside the group.</param>
    /// <returns>An event scope bound to the supplied group and agent identifiers.</returns>
    IAgentEventScope CreateScope(string groupKey, string agentKey);

    /// <summary>
    /// Publishes the creation of an agent group before individual agent scopes begin emitting transcript events.
    /// </summary>
    /// <param name="groupKey">Stable key of the created agent group.</param>
    /// <param name="displayName">Display name shown for the created agent group.</param>
    /// <param name="cancellationToken">Cancels event publication.</param>
    ValueTask PublishGroupCreatedAsync(
        string groupKey,
        string displayName,
        CancellationToken cancellationToken = default);
}
