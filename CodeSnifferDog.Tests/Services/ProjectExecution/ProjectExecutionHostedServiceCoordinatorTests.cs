using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Recovery;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectExecutionHostedServiceCoordinatorTests
{
    [TestMethod]
    public async Task StartAsync_WhenNotReady_RunsRecoveryBeforeReadiness_AndDoesNotClaim()
    {
        List<string> calls = [];
        TestRecoveryService recoveryService = new(calls);
        TestReadinessGate readinessGate = new(calls, new ExecutionReadinessResult(false, "not ready"));
        TestQueueClaimer queueClaimer = new(calls, null);
        TestClaimExecutor claimExecutor = new(calls);
        TestQueueLock queueLock = new(calls);
        using ProjectExecutionHostedService service = CreateService(
            readinessGate,
            queueClaimer,
            claimExecutor,
            recoveryService,
            queueLock);

        await service.StartAsync(CancellationToken.None);
        await readinessGate.WaitForCheckAsync();
        await service.StopAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "recovery", "readiness" }, calls);
        Assert.AreEqual(0, queueClaimer.CallCount);
        Assert.AreEqual(0, queueLock.AcquireCount);
        Assert.AreEqual(0, claimExecutor.CallCount);
    }

    [TestMethod]
    public async Task StartAsync_WhenClaimExists_AcquiresQueueLockAroundClaim_AndExecutesClaim()
    {
        List<string> calls = [];
        TestRecoveryService recoveryService = new(calls);
        TestReadinessGate readinessGate = new(calls, new ExecutionReadinessResult(true, null));
        using ProjectExecutionLease lease = new(Guid.NewGuid(), CancellationToken.None, static _ => { });
        ProjectExecutionClaim claim = new(lease.ProjectId, "uploads/repo.zip", lease);
        using CancellationTokenSource stopSource = new();
        TestQueueClaimer queueClaimer = new(calls, claim);
        TestClaimExecutor claimExecutor = new(calls, stopSource);
        TestQueueLock queueLock = new(calls);
        using ProjectExecutionHostedService service = CreateService(
            readinessGate,
            queueClaimer,
            claimExecutor,
            recoveryService,
            queueLock);

        await service.StartAsync(stopSource.Token);
        await claimExecutor.WaitForExecuteAsync();
        await service.StopAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "recovery",
                "readiness",
                "lock-acquire",
                "claim",
                "lock-dispose",
                "execute",
            },
            calls);
        Assert.AreSame(claim, claimExecutor.Claims.Single());
        Assert.AreEqual(1, queueLock.AcquireCount);
    }

    private static ProjectExecutionHostedService CreateService(
        IExecutionReadinessGate readinessGate,
        IExecutionQueueClaimer queueClaimer,
        IClaimExecutor claimExecutor,
        IInterruptedProjectRecoveryService recoveryService,
        IProjectExecutionQueueLock queueLock) =>
        new(
            readinessGate,
            queueClaimer,
            claimExecutor,
            recoveryService,
            queueLock,
            Options.Create(new ProjectExecutionOptions
            {
                MaxConcurrentWorkers = 1,
                QueuePollingIntervalSeconds = 60,
            }),
            NullLogger<ProjectExecutionHostedService>.Instance);

    private sealed class TestRecoveryService(List<string> calls) : IInterruptedProjectRecoveryService
    {
        public Task RecoverAsync(CancellationToken cancellationToken)
        {
            calls.Add("recovery");
            return Task.CompletedTask;
        }
    }

    private sealed class TestReadinessGate(
        List<string> calls,
        ExecutionReadinessResult result) : IExecutionReadinessGate
    {
        private readonly TaskCompletionSource _checked = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ExecutionReadinessResult Check()
        {
            calls.Add("readiness");
            _checked.TrySetResult();
            return result;
        }

        public Task WaitForCheckAsync() => _checked.Task;
    }

    private sealed class TestQueueClaimer(
        List<string> calls,
        ProjectExecutionClaim? claim) : IExecutionQueueClaimer
    {
        public int CallCount { get; private set; }

        public Task<ProjectExecutionClaim?> TryClaimNextAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            calls.Add("claim");
            return Task.FromResult(claim);
        }
    }

    private sealed class TestClaimExecutor(
        List<string> calls,
        CancellationTokenSource? stopSource = null) : IClaimExecutor
    {
        private readonly TaskCompletionSource _executed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public List<ProjectExecutionClaim> Claims { get; } = [];

        public Task ExecuteAsync(
            int workerNumber,
            ProjectExecutionClaim claim,
            CancellationToken stoppingToken)
        {
            CallCount++;
            Claims.Add(claim);
            calls.Add("execute");
            stopSource?.Cancel();
            _executed.TrySetResult();
            return Task.CompletedTask;
        }

        public Task WaitForExecuteAsync() => _executed.Task;
    }

    private sealed class TestQueueLock(List<string> calls) : IProjectExecutionQueueLock
    {
        public int AcquireCount { get; private set; }

        public Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
        {
            AcquireCount++;
            calls.Add("lock-acquire");
            return Task.FromResult<IDisposable>(new TestLockHandle(calls));
        }
    }

    private sealed class TestLockHandle(List<string> calls) : IDisposable
    {
        public void Dispose()
        {
            calls.Add("lock-dispose");
        }
    }
}
