using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

public interface IProjectAgentStatusLiveBackfillService
{
    Task<IReadOnlyList<ProjectAgentLiveUpdateDto>> GetBackfillAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default);
}
