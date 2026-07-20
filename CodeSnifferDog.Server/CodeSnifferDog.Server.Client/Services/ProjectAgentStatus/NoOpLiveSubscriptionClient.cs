using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;

/// <summary>
/// Provides a no-op live subscription client for environments where live updates are disabled.
/// </summary>
public sealed class NoOpLiveSubscriptionClient : ILiveSubscriptionClient
{
    /// <inheritdoc />
    public Task SubscribeAsync(
        LiveSubscriptionRequestDto request,
        Func<IReadOnlyList<LiveUpdateDto>, Task> onUpdates,
        Func<Task> onReconnecting,
        Func<Task> onReconnectRequired,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task UnsubscribeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() =>
        ValueTask.CompletedTask;
}
