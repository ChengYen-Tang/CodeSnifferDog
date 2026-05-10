using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

public interface IProjectAgentStatusLiveSubscriptionClient : IAsyncDisposable
{
    Task SubscribeAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        Func<ProjectAgentLiveUpdateDto, Task> onUpdate,
        CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(CancellationToken cancellationToken = default);
}
