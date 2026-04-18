namespace CodeSnifferDog.Modules.Concurrency;

public sealed class ReviewAgentConcurrencyGate : IReviewAgentConcurrencyGate, IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public ReviewAgentConcurrencyGate(int maxParallelAgents)
    {
        if (maxParallelAgents <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxParallelAgents), "Max parallel agents must be greater than zero.");

        MaxParallelAgents = maxParallelAgents;
        _semaphore = new SemaphoreSlim(maxParallelAgents);
    }

    public int MaxParallelAgents { get; }

    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(_semaphore);
    }

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

    public void Dispose() => _semaphore.Dispose();

    private sealed class Lease(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore = semaphore;
        private bool _disposed;

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
