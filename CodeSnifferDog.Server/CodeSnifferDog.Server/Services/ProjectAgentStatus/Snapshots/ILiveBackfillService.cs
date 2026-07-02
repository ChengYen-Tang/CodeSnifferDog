using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;

public interface ILiveBackfillService
{
    Task<IReadOnlyList<ProjectAgentLiveUpdateDto>> GetBackfillAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default);
}
