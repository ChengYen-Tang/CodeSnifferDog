namespace CodeSnifferDog.Modules.Concurrency;

/// <summary>
/// Implements a semaphore-based concurrency gate for review agents.
/// </summary>
public sealed class ReviewAgentConcurrencyGate : IReviewAgentConcurrencyGate, IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewAgentConcurrencyGate"/> class.
    /// </summary>
    /// <param name="maxParallelAgents">Maximum number of concurrently acquired slots.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxParallelAgents"/> is not greater than zero.</exception>
    public ReviewAgentConcurrencyGate(int maxParallelAgents)
    {
        if (maxParallelAgents <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxParallelAgents), "Max parallel agents must be greater than zero.");

        MaxParallelAgents = maxParallelAgents;
        _semaphore = new SemaphoreSlim(maxParallelAgents);
    }

    /// <summary>
    /// Gets the configured maximum number of concurrent review agents.
    /// </summary>
    public int MaxParallelAgents { get; }

    /// <inheritdoc />
    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(_semaphore);
    }

    /// <inheritdoc />
    public bool TryAcquire(out IAsyncDisposable? lease)
    {
        if (!_semaphore.Wait(0))
        {
            lease = null;
            return false;
        }

        lease = new Lease(_semaphore);
        return true;
    }

    /// <inheritdoc />
    public void Dispose() => _semaphore.Dispose();

    /// <summary>
    /// Releases a concurrency slot when disposed.
    /// </summary>
    private sealed class Lease(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore = semaphore;
        private bool _disposed;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _disposed = true;
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
