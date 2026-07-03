using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;

public interface ILiveSubscriptionClient : IAsyncDisposable
{
    Task SubscribeAsync(
        LiveSubscriptionRequestDto request,
        Func<LiveUpdateDto, Task> onUpdate,
        Func<Task> onReconnecting,
        Func<Task> onReconnectRequired,
        CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(CancellationToken cancellationToken = default);
}
