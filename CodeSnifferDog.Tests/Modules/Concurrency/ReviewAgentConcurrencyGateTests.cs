using CodeSnifferDog.Modules.Concurrency;

namespace CodeSnifferDog.Tests.Modules.Concurrency;

[TestClass]
public sealed class ReviewAgentConcurrencyGateTests
{
    [TestMethod]
    public async Task AcquireAsync_RespectsConfiguredParallelLimit()
    {
        using ReviewAgentConcurrencyGate gate = new(2);
        int currentConcurrency = 0;
        int maxObservedConcurrency = 0;

        Task[] tasks = Enumerable.Range(0, 6)
            .Select(_ => Task.Run(async () =>
            {
                await using IAsyncDisposable lease = await gate.AcquireAsync(CancellationToken.None);
                int newConcurrency = Interlocked.Increment(ref currentConcurrency);
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, newConcurrency);

                try
                {
                    await Task.Delay(40);
                }
                finally
                {
                    Interlocked.Decrement(ref currentConcurrency);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.AreEqual(2, maxObservedConcurrency);
    }

    [TestMethod]
    public void Constructor_Throws_WhenMaxParallelAgentsIsInvalid()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ReviewAgentConcurrencyGate(0));
    }

    [TestMethod]
    public async Task TryAcquire_ReturnsFalse_WhenAllSlotsAreInUse()
    {
        using ReviewAgentConcurrencyGate gate = new(1);
        await using IAsyncDisposable firstLease = await gate.AcquireAsync(CancellationToken.None);

        bool acquired = gate.TryAcquire(out IAsyncDisposable? secondLease);

        Assert.IsFalse(acquired);
        Assert.IsNull(secondLease);
    }
}
