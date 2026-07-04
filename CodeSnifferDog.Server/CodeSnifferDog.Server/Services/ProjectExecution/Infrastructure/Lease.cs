using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

/// <summary>
/// Represents the lifetime and cancellation state of a running project execution.
/// </summary>
public sealed class Lease : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<Guid> _onDispose;
    private readonly Lock _syncRoot = new();
    private readonly CancellationTokenRegistration _hostStoppingRegistration;
    private int _cancellationSource = (int)Source.None;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Lease"/> class.
    /// </summary>
    /// <param name="projectId">Project identifier associated with the lease.</param>
    /// <param name="hostStoppingToken">Host shutdown token that cancels the lease during service shutdown.</param>
    /// <param name="onDispose">Callback that removes the lease from its registry.</param>
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

    /// <summary>
    /// Gets the project identifier associated with this lease.
    /// </summary>
    public Guid ProjectId { get; }

    /// <summary>
    /// Gets the cancellation token observed by the running project execution.
    /// </summary>
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    /// <summary>
    /// Gets the source that triggered cancellation.
    /// </summary>
    internal Source CancellationSource =>
        (Source)Volatile.Read(ref _cancellationSource);

    /// <summary>
    /// Gets a task that completes when the lease is disposed.
    /// </summary>
    internal Task Completion => _completionSource.Task;

    /// <summary>
    /// Attempts to cancel the running execution.
    /// </summary>
    /// <param name="source">Source that initiated cancellation.</param>
    /// <returns><see langword="true"/> when cancellation was requested; otherwise, <see langword="false"/>.</returns>
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

    /// <inheritdoc />
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
