namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

/// <summary>
/// Provides a polling-based refresh fallback when live push updates are unavailable.
/// </summary>
public interface IPollingFallback : IAsyncDisposable
{
    /// <summary>
    /// Gets whether the polling fallback is currently active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Starts periodic refresh callbacks.
    /// </summary>
    /// <param name="onRefreshRequested">Callback invoked for each polling refresh tick.</param>
    /// <param name="cancellationToken">Cancels polling.</param>
    void Start(Func<CancellationToken, Task> onRefreshRequested, CancellationToken cancellationToken);

    /// <summary>
    /// Stops periodic refresh callbacks.
    /// </summary>
    void Stop();
}
