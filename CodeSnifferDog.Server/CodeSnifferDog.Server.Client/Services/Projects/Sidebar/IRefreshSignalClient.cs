namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

/// <summary>
/// Delivers push-based refresh signals and connection-state changes for the projects sidebar.
/// </summary>
public interface IRefreshSignalClient : IAsyncDisposable
{
    /// <summary>
    /// Starts the refresh-signal client.
    /// </summary>
    /// <param name="onRefreshRequested">Callback invoked when a push refresh is requested.</param>
    /// <param name="onConnectionStateChanged">Callback invoked when push-connection state changes.</param>
    /// <param name="cancellationToken">Cancels startup.</param>
    Task StartAsync(
        Func<CancellationToken, Task> onRefreshRequested,
        Action<bool, bool, string?> onConnectionStateChanged,
        CancellationToken cancellationToken = default);
}
