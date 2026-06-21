using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

internal interface IProjectAgentStatusBackfillQueryService
{
    Task<ProjectAgentStatusBackfillReadModel> GetBackfillAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default);
}
