namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class ProjectExecutionLease : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<Guid> _onDispose;
    private readonly Lock _syncRoot = new();
    private bool _disposed;

    internal ProjectExecutionLease(Guid projectId, CancellationTokenSource cancellationTokenSource, Action<Guid> onDispose)
    {
        ProjectId = projectId;
        _cancellationTokenSource = cancellationTokenSource;
        _onDispose = onDispose;
    }

    public Guid ProjectId { get; }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    internal Task Completion => _completionSource.Task;

    internal bool TryCancel()
    {
        lock (_syncRoot)
        {
            if (_disposed)
                return false;

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
            _completionSource.TrySetResult();
            _onDispose(ProjectId);
            _cancellationTokenSource.Dispose();
        }
    }
}
