using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

public sealed class Lease : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<Guid> _onDispose;
    private readonly Lock _syncRoot = new();
    private readonly CancellationTokenRegistration _hostStoppingRegistration;
    private int _cancellationSource = (int)Source.None;
    private bool _disposed;

    internal Lease(Guid projectId, CancellationToken hostStoppingToken, Action<Guid> onDispose)
    {
        ProjectId = projectId;
        _onDispose = onDispose;
        _hostStoppingRegistration = hostStoppingToken.Register(static state =>
        {
            Lease lease = (Lease)state!;
            lease.TryCancel(Source.HostShutdown);
        }, this);
    }

    public Guid ProjectId { get; }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    internal Source CancellationSource =>
        (Source)Volatile.Read(ref _cancellationSource);

    internal Task Completion => _completionSource.Task;

    internal bool TryCancel(Source source)
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
