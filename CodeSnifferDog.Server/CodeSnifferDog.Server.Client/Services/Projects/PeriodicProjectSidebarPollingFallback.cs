namespace CodeSnifferDog.Server.Client.Services.Projects;

public sealed class PeriodicProjectSidebarPollingFallback(TimeSpan? interval = null) : IProjectSidebarPollingFallback
{
    private readonly TimeSpan _interval = interval ?? TimeSpan.FromSeconds(15);
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cancellationTokenSource;

    public bool IsActive { get; private set; }

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

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}
