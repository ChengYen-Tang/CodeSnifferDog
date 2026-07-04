namespace CodeSnifferDog.Modules.Concurrency;

/// <summary>
/// Limits the number of review agents that may run concurrently.
/// </summary>
public interface IReviewAgentConcurrencyGate
{
    /// <summary>
    /// Asynchronously acquires one concurrency slot.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels waiting for a slot.</param>
    /// <returns>An async-disposable lease that releases the slot.</returns>
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to acquire one concurrency slot immediately.
    /// </summary>
    /// <param name="lease">Async-disposable lease that releases the slot when acquisition succeeds.</param>
    /// <returns><see langword="true"/> when a slot was acquired; otherwise, <see langword="false"/>.</returns>
    bool TryAcquire(out IAsyncDisposable? lease);
}
