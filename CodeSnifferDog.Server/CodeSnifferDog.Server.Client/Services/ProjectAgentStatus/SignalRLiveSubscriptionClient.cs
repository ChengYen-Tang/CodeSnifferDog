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
    private readonly SemaphoreSlim _subscriptionTransitionLock = new(1, 1);
    private readonly object _updateBufferLock = new();
    private readonly List<LiveUpdateDto> _bufferedUpdates = [];
    private Task _updateDispatchTail = Task.CompletedTask;
    private HubConnection? _connection;
    private Guid? _subscribedProjectId;
    private Guid? _subscribedAgentId;
    private Func<IReadOnlyList<LiveUpdateDto>, Task>? _onUpdates;
    private Func<Task>? _onReconnecting;
    private Func<Task>? _onReconnectRequired;
    private bool _isBufferingUpdates;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="request" />, <paramref name="onUpdates" />, <paramref name="onReconnecting" />, or <paramref name="onReconnectRequired" /> is <see langword="null" />.</exception>
    public async Task SubscribeAsync(
        LiveSubscriptionRequestDto request,
        Func<IReadOnlyList<LiveUpdateDto>, Task> onUpdates,
        Func<Task> onReconnecting,
        Func<Task> onReconnectRequired,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onUpdates);
        ArgumentNullException.ThrowIfNull(onReconnecting);
        ArgumentNullException.ThrowIfNull(onReconnectRequired);

        await _subscriptionTransitionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Task precedingDispatch = BeginBufferingUpdates(
                onUpdates,
                onReconnecting,
                onReconnectRequired);
            try
            {
                await AwaitPrecedingDispatchAsync(precedingDispatch, cancellationToken).ConfigureAwait(false);
                await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

                bool includeProjectState = _subscribedProjectId != request.ProjectId;
                if (_subscribedProjectId is Guid subscribedProjectId && subscribedProjectId != request.ProjectId)
                {
                    await _connection!
                        .InvokeAsync(
                            ProjectUpdatesContract.UnsubscribeFromProjectMethodName,
                            subscribedProjectId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                LiveSubscriptionRequestDto effectiveRequest = new()
                {
                    ProjectId = request.ProjectId,
                    SnapshotGeneratedAtUtc = request.SnapshotGeneratedAtUtc,
                    AgentId = request.AgentId,
                    LatestSequence = request.LatestSequence,
                    IncludeProjectState = includeProjectState,
                };
                LiveUpdateDto[] backfill = await _connection!
                    .InvokeAsync<LiveUpdateDto[]>(
                        ProjectUpdatesContract.SubscribeToProjectMethodName,
                        effectiveRequest,
                        cancellationToken)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                _subscribedProjectId = request.ProjectId;
                _subscribedAgentId = request.AgentId;
                await FlushBufferedUpdatesAsync(backfill)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                StopBufferingUpdates();
                await ResetConnectionAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _subscriptionTransitionLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        await _subscriptionTransitionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                if (_connection is not null &&
                    _subscribedProjectId is Guid subscribedProjectId &&
                    _connection.State == HubConnectionState.Connected)
                {
                    await _connection
                        .InvokeAsync(
                            ProjectUpdatesContract.UnsubscribeFromProjectMethodName,
                            subscribedProjectId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                _subscribedProjectId = null;
                _subscribedAgentId = null;
                StopBufferingUpdates();
            }
            catch
            {
                StopBufferingUpdates();
                await ResetConnectionAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _subscriptionTransitionLock.Release();
        }
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

            _connection.On<LiveUpdateDto>(
                ProjectUpdatesContract.AgentStatusUpdatedMethodName,
                HandleIncomingUpdateAsync);

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
        await _subscriptionTransitionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _onUpdates = null;
            _onReconnecting = null;
            _onReconnectRequired = null;
            StopBufferingUpdates();

            await ResetConnectionAsync().ConfigureAwait(false);
        }
        finally
        {
            _subscriptionTransitionLock.Release();
        }
    }

    /// <summary>
    /// Buffers a live update while subscription catch-up is being established, or dispatches it immediately afterward.
    /// </summary>
    private Task HandleIncomingUpdateAsync(LiveUpdateDto update)
    {
        lock (_updateBufferLock)
        {
            if (_isBufferingUpdates)
            {
                _bufferedUpdates.Add(update);
                return Task.CompletedTask;
            }

            return QueueUpdatesLocked([update]);
        }
    }

    /// <summary>
    /// Starts buffering live events so they can be applied after the server catch-up result.
    /// </summary>
    /// <param name="onUpdates">Handler that receives ordered update batches.</param>
    /// <param name="onReconnecting">Handler invoked while SignalR reconnects.</param>
    /// <param name="onReconnectRequired">Handler invoked after the connection must be re-established.</param>
    /// <returns>The dispatch task that was queued before buffering began.</returns>
    private Task BeginBufferingUpdates(
        Func<IReadOnlyList<LiveUpdateDto>, Task> onUpdates,
        Func<Task> onReconnecting,
        Func<Task> onReconnectRequired)
    {
        lock (_updateBufferLock)
        {
            _onUpdates = onUpdates;
            _onReconnecting = onReconnecting;
            _onReconnectRequired = onReconnectRequired;
            _bufferedUpdates.Clear();
            _isBufferingUpdates = true;
            return _updateDispatchTail;
        }
    }

    /// <summary>
    /// Waits for callbacks queued before a subscription transition so stale callbacks cannot overlap the next handler.
    /// </summary>
    private static async Task AwaitPrecedingDispatchAsync(
        Task precedingDispatch,
        CancellationToken cancellationToken)
    {
        try
        {
            await precedingDispatch.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // The original callback owner observes its failure. A later subscription can still recover.
        }
    }

    /// <summary>
    /// Applies the catch-up result first, then drains live events received while it was loading.
    /// </summary>
    private Task FlushBufferedUpdatesAsync(IReadOnlyList<LiveUpdateDto> backfill)
    {
        lock (_updateBufferLock)
        {
            IReadOnlyList<LiveUpdateDto> initialBatch;
            if (backfill.Count == 0)
            {
                initialBatch = _bufferedUpdates.ToArray();
            }
            else if (_bufferedUpdates.Count == 0)
            {
                initialBatch = backfill;
            }
            else
            {
                LiveUpdateDto[] combinedUpdates = new LiveUpdateDto[backfill.Count + _bufferedUpdates.Count];
                for (int index = 0; index < backfill.Count; index++)
                    combinedUpdates[index] = backfill[index];

                _bufferedUpdates.CopyTo(combinedUpdates, backfill.Count);
                initialBatch = combinedUpdates;
            }

            _bufferedUpdates.Clear();
            _isBufferingUpdates = false;

            // Queue the captured catch-up batch before exposing live dispatch. Updates that arrive
            // after this lock is released are appended behind it, but are not awaited by SubscribeAsync.
            return QueueUpdatesLocked(initialBatch);
        }
    }

    /// <summary>
    /// Appends one update batch to the serialized dispatch queue. The caller must hold <see cref="_updateBufferLock" />.
    /// </summary>
    private Task QueueUpdatesLocked(IReadOnlyList<LiveUpdateDto> updates)
    {
        Func<IReadOnlyList<LiveUpdateDto>, Task>? handler = _onUpdates;
        if (handler is null)
            return Task.CompletedTask;

        if (updates.Count == 0)
            return _updateDispatchTail;

        Task queuedDispatch = DispatchAfterAsync(_updateDispatchTail, handler, updates);
        _updateDispatchTail = queuedDispatch;
        return queuedDispatch;
    }

    /// <summary>
    /// Dispatches a batch after the preceding callback has completed while allowing the queue to recover from a prior callback failure.
    /// </summary>
    private static async Task DispatchAfterAsync(
        Task precedingDispatch,
        Func<IReadOnlyList<LiveUpdateDto>, Task> handler,
        IReadOnlyList<LiveUpdateDto> updates)
    {
        try
        {
            await precedingDispatch.ConfigureAwait(false);
        }
        catch
        {
            // A failed callback is observed by its original caller. Keep later live updates flowing.
        }

        await handler(updates).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops buffering and discards catch-up events after a failed or canceled transition.
    /// </summary>
    private void StopBufferingUpdates()
    {
        lock (_updateBufferLock)
        {
            _isBufferingUpdates = false;
            _bufferedUpdates.Clear();
        }
    }

    /// <summary>
    /// Disposes the current transport so SignalR removes every server-side group membership.
    /// </summary>
    private async Task ResetConnectionAsync()
    {
        HubConnection? connection = _connection;
        _connection = null;
        _subscribedProjectId = null;
        _subscribedAgentId = null;

        lock (_updateBufferLock)
        {
            _isBufferingUpdates = false;
            _bufferedUpdates.Clear();
            _onUpdates = null;
            _onReconnecting = null;
            _onReconnectRequired = null;
        }

        if (connection is not null)
            await connection.DisposeAsync().ConfigureAwait(false);
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
