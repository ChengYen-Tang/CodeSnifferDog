using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;

public sealed class NoOpLiveSubscriptionClient : ILiveSubscriptionClient
{
    public Task SubscribeAsync(
        LiveSubscriptionRequestDto request,
        Func<LiveUpdateDto, Task> onUpdate,
        Func<Task> onReconnecting,
        Func<Task> onReconnectRequired,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UnsubscribeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() =>
        ValueTask.CompletedTask;
}
