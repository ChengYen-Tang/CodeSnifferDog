namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

/// <summary>
/// Implements the queue lock used to prevent concurrent queue claims.
/// </summary>
public sealed class QueueLock : IQueueLock, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <inheritdoc />
    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    /// <inheritdoc />
    public void Dispose() => _semaphore.Dispose();

    /// <summary>
    /// Releases the semaphore when the lock scope ends.
    /// </summary>
    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private bool _disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            semaphore.Release();
        }
    }
}
