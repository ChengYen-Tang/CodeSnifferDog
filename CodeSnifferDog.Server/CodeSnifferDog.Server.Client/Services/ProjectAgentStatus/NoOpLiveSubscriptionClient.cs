using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;

public sealed class NoOpLiveSubscriptionClient : ILiveSubscriptionClient
{
    public Task SubscribeAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        Func<ProjectAgentLiveUpdateDto, Task> onUpdate,
        Func<Task> onReconnecting,
        Func<Task> onReconnectRequired,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UnsubscribeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() =>
        ValueTask.CompletedTask;
}
