using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.SignalR.Client;

namespace CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;

/// <summary>
/// Uses SignalR to receive live agent-status updates for the agent-status page.
/// </summary>
/// <param name="httpClient">HTTP client whose base address is used to connect to the project updates hub.</param>
public sealed class SignalRLiveSubscriptionClient(HttpClient httpClient) : ILiveSubscriptionClient
{
    private readonly HttpClient _httpClient = httpClient;
    private HubConnection? _connection;
    private Guid? _subscribedProjectId;
    private Guid? _subscribedAgentId;
    private Func<LiveUpdateDto, Task>? _onUpdate;
    private Func<Task>? _onReconnecting;
    private Func<Task>? _onReconnectRequired;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="request" />, <paramref name="onUpdate" />, <paramref name="onReconnecting" />, or <paramref name="onReconnectRequired" /> is <see langword="null" />.</exception>
    public async Task SubscribeAsync(
        LiveSubscriptionRequestDto request,
        Func<LiveUpdateDto, Task> onUpdate,
        Func<Task> onReconnecting,
        Func<Task> onReconnectRequired,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onUpdate);
        ArgumentNullException.ThrowIfNull(onReconnecting);
        ArgumentNullException.ThrowIfNull(onReconnectRequired);

        _onUpdate = onUpdate;
        _onReconnecting = onReconnecting;
        _onReconnectRequired = onReconnectRequired;
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (_subscribedProjectId is Guid subscribedProjectId)
        {
            if (subscribedProjectId == request.ProjectId)
            {
                await _connection!.InvokeAsync(ProjectUpdatesContract.SubscribeToProjectMethodName, request, cancellationToken).ConfigureAwait(false);
                _subscribedAgentId = request.AgentId;
                return;
            }

            await _connection!.InvokeAsync(ProjectUpdatesContract.UnsubscribeFromProjectMethodName, subscribedProjectId, cancellationToken).ConfigureAwait(false);
        }

        await _connection!.InvokeAsync(ProjectUpdatesContract.SubscribeToProjectMethodName, request, cancellationToken).ConfigureAwait(false);
        _subscribedProjectId = request.ProjectId;
        _subscribedAgentId = request.AgentId;
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null || _subscribedProjectId is null || _connection.State != HubConnectionState.Connected)
        {
            _subscribedProjectId = null;
            _subscribedAgentId = null;
            return;
        }

        await _connection.InvokeAsync(ProjectUpdatesContract.UnsubscribeFromProjectMethodName, _subscribedProjectId.Value, cancellationToken).ConfigureAwait(false);
        _subscribedProjectId = null;
        _subscribedAgentId = null;
    }

    /// <summary>
    /// Ensures the SignalR connection exists and is started.
    /// </summary>
    /// <param name="cancellationToken">Cancels connection startup.</param>
    private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri(_httpClient.BaseAddress!, ProjectUpdatesContract.HubPath))
                .WithAutomaticReconnect()
                .Build();

            _connection.On<LiveUpdateDto>(ProjectUpdatesContract.AgentStatusUpdatedMethodName, update =>
            {
                Func<LiveUpdateDto, Task>? handler = _onUpdate;
                return handler is null ? Task.CompletedTask : handler(update);
            });

            _connection.Reconnecting += _ => NotifyReconnectingAsync();
            _connection.Reconnected += _ => NotifyReconnectRequiredAsync();
            _connection.Closed += _ => NotifyReconnectRequiredAsync();
        }

        if (_connection.State == HubConnectionState.Connected)
            return;

        await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _subscribedProjectId = null;
        _subscribedAgentId = null;
        _onUpdate = null;
        _onReconnecting = null;
        _onReconnectRequired = null;
    }

    /// <summary>
    /// Invokes the reconnecting callback when one is registered.
    /// </summary>
    /// <returns>A completed task when no callback is registered; otherwise the callback task.</returns>
    private Task NotifyReconnectingAsync()
    {
        Func<Task>? handler = _onReconnecting;
        return handler is null ? Task.CompletedTask : handler();
    }

    /// <summary>
    /// Invokes the reconnect-required callback when one is registered.
    /// </summary>
    /// <returns>A completed task when no callback is registered; otherwise the callback task.</returns>
    private Task NotifyReconnectRequiredAsync()
    {
        Func<Task>? handler = _onReconnectRequired;
        return handler is null ? Task.CompletedTask : handler();
    }
}
