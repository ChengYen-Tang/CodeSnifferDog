using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;

/// <summary>
/// Manages live agent-status subscriptions for the agent-status page.
/// </summary>
public interface ILiveSubscriptionClient : IAsyncDisposable
{
    /// <summary>
    /// Subscribes to live updates for one project and optional selected agent.
    /// </summary>
    /// <param name="request">Subscription request describing the target project and optional selected agent.</param>
    /// <param name="onUpdate">Callback invoked for each received live update.</param>
    /// <param name="onReconnecting">Callback invoked when the underlying transport starts reconnecting.</param>
    /// <param name="onReconnectRequired">Callback invoked when the client must resubscribe after reconnection or closure.</param>
    /// <param name="cancellationToken">Cancels subscription startup.</param>
    Task SubscribeAsync(
        LiveSubscriptionRequestDto request,
        Func<LiveUpdateDto, Task> onUpdate,
        Func<Task> onReconnecting,
        Func<Task> onReconnectRequired,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from the current live project subscription, if one exists.
    /// </summary>
    /// <param name="cancellationToken">Cancels the unsubscribe request.</param>
    Task UnsubscribeAsync(CancellationToken cancellationToken = default);
}
