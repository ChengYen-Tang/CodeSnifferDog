namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

/// <summary>
/// Serializes access to queue-claim operations across background workers.
/// </summary>
public interface IQueueLock
{
    /// <summary>
    /// Acquires the queue lock.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels waiting for the lock.</param>
    /// <returns>An <see cref="IDisposable"/> that releases the lock when disposed.</returns>
    Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
