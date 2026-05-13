using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

public interface IProjectAgentStatusLiveSubscriptionClient : IAsyncDisposable
{
    Task SubscribeAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        Func<ProjectAgentLiveUpdateDto, Task> onUpdate,
        Func<Task> onReconnecting,
        Func<Task> onReconnectRequired,
        CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(CancellationToken cancellationToken = default);
}
