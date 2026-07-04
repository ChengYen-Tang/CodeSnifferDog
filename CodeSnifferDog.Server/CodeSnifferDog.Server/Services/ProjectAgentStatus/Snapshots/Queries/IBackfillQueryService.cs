using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

/// <summary>
/// Loads persisted read models required for live-update backfill.
/// </summary>
internal interface IBackfillQueryService
{
    /// <summary>
    /// Loads the persisted rows required to backfill one live subscription.
    /// </summary>
    /// <param name="request">Subscription request describing the target project and latest known sequence.</param>
    /// <param name="cancellationToken">Cancels query execution.</param>
    /// <returns>The backfill read model.</returns>
    Task<BackfillReadModel> GetBackfillAsync(
        LiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default);
}
