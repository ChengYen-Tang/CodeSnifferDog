namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

/// <summary>
/// Periodically requests sidebar refreshes when push-based live updates are unavailable.
/// </summary>
/// <param name="interval">Optional polling interval; defaults to 15 seconds.</param>
public sealed class PeriodicPollingFallback(TimeSpan? interval = null) : IPollingFallback
{
    private readonly TimeSpan _interval = interval ?? TimeSpan.FromSeconds(15);
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <inheritdoc />
    public bool IsActive { get; private set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="onRefreshRequested" /> is <see langword="null" />.</exception>
    public void Start(Func<CancellationToken, Task> onRefreshRequested, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onRefreshRequested);

        if (IsActive)
            return;

        IsActive = true;
        _timer = new PeriodicTimer(_interval);
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = RunAsync(onRefreshRequested, _cancellationTokenSource.Token);
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!IsActive)
            return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _timer?.Dispose();
        _timer = null;
        IsActive = false;
    }

    /// <summary>
    /// Runs the polling loop until cancellation or timer disposal.
    /// </summary>
    /// <param name="onRefreshRequested">Callback invoked for each polling refresh tick.</param>
    /// <param name="cancellationToken">Cancels the polling loop.</param>
    private async Task RunAsync(Func<CancellationToken, Task> onRefreshRequested, CancellationToken cancellationToken)
    {
        if (_timer is null)
            return;

        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await onRefreshRequested(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}
