namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class ProjectExecutionLease : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<Guid> _onDispose;
    private readonly Lock _syncRoot = new();
    private readonly CancellationTokenRegistration _hostStoppingRegistration;
    private int _cancellationSource = (int)ProjectExecutionCancellationSource.None;
    private bool _disposed;

    internal ProjectExecutionLease(Guid projectId, CancellationToken hostStoppingToken, Action<Guid> onDispose)
    {
        ProjectId = projectId;
        _onDispose = onDispose;
        _hostStoppingRegistration = hostStoppingToken.Register(static state =>
        {
            ProjectExecutionLease lease = (ProjectExecutionLease)state!;
            lease.TryCancel(ProjectExecutionCancellationSource.HostShutdown);
        }, this);
    }

    public Guid ProjectId { get; }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    internal ProjectExecutionCancellationSource CancellationSource =>
        (ProjectExecutionCancellationSource)Volatile.Read(ref _cancellationSource);

    internal Task Completion => _completionSource.Task;

    internal bool TryCancel(ProjectExecutionCancellationSource source)
    {
        lock (_syncRoot)
        {
            if (_disposed || _cancellationTokenSource.IsCancellationRequested)
                return false;

            _cancellationSource = (int)source;
            _cancellationTokenSource.Cancel();
            return true;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
                return;

            _disposed = true;
            _hostStoppingRegistration.Dispose();
            _completionSource.TrySetResult();
            _onDispose(ProjectId);
            _cancellationTokenSource.Dispose();
        }
    }
}
