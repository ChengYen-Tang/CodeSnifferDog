namespace CodeSnifferDog.Modules.Concurrency;

public interface IReviewAgentConcurrencyGate
{
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken);

    bool TryAcquire(out IAsyncDisposable? lease);
}
