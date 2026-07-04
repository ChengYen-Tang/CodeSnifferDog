using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;

/// <summary>
/// Builds live-update backfill sequences for newly connected clients.
/// </summary>
public interface ILiveBackfillService
{
    /// <summary>
    /// Loads live updates that should be replayed to catch a client up to current state.
    /// </summary>
    /// <param name="request">Subscription request describing the project, selected agent, and latest known sequence.</param>
    /// <param name="cancellationToken">Cancels backfill loading.</param>
    /// <returns>The live updates required to backfill the client.</returns>
    Task<IReadOnlyList<LiveUpdateDto>> GetBackfillAsync(
        LiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default);
}
