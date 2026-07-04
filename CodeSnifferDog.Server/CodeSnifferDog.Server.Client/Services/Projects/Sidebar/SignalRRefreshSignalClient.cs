using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.SignalR.Client;

namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

/// <summary>
/// Uses SignalR to receive push-based sidebar refresh notifications and connection-state changes.
/// </summary>
/// <param name="httpClient">HTTP client whose base address is used to connect to the project updates hub.</param>
public sealed class SignalRRefreshSignalClient(HttpClient httpClient) : IRefreshSignalClient
{
    private readonly HttpClient _httpClient = httpClient;
    private HubConnection? _connection;
    private Func<CancellationToken, Task>? _onRefreshRequested;
    private Action<bool, bool, string?>? _onConnectionStateChanged;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="onRefreshRequested" /> or <paramref name="onConnectionStateChanged" /> is <see langword="null" />.</exception>
    public async Task StartAsync(
        Func<CancellationToken, Task> onRefreshRequested,
        Action<bool, bool, string?> onConnectionStateChanged,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onRefreshRequested);
        ArgumentNullException.ThrowIfNull(onConnectionStateChanged);

        _onRefreshRequested = onRefreshRequested;
        _onConnectionStateChanged = onConnectionStateChanged;

        if (_connection is null)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri(_httpClient.BaseAddress!, ProjectUpdatesContract.HubPath))
                .WithAutomaticReconnect()
                .Build();

            _connection.On(ProjectUpdatesContract.ProjectsChangedMethodName, () => NotifyRefreshRequestedAsync(CancellationToken.None));
            _connection.Reconnecting += _ =>
            {
                _onConnectionStateChanged?.Invoke(false, true, "Live updates reconnecting...");
                return Task.CompletedTask;
            };
            _connection.Reconnected += async _ =>
            {
                _onConnectionStateChanged?.Invoke(true, false, null);
                await NotifyRefreshRequestedAsync(CancellationToken.None).ConfigureAwait(false);
            };
            _connection.Closed += _ =>
            {
                _onConnectionStateChanged?.Invoke(false, false, "Live updates unavailable.");
                return Task.CompletedTask;
            };
        }

        if (_connection.State == HubConnectionState.Connected)
        {
            _onConnectionStateChanged.Invoke(true, false, null);
            return;
        }

        await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
        _onConnectionStateChanged.Invoke(true, false, null);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _onRefreshRequested = null;
        _onConnectionStateChanged = null;
    }

    /// <summary>
    /// Invokes the current refresh-request handler, when one is registered.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token forwarded to the handler.</param>
    /// <returns>A completed task when no handler is registered; otherwise the handler task.</returns>
    private Task NotifyRefreshRequestedAsync(CancellationToken cancellationToken)
    {
        Func<CancellationToken, Task>? handler = _onRefreshRequested;
        return handler is null ? Task.CompletedTask : handler(cancellationToken);
    }
}
