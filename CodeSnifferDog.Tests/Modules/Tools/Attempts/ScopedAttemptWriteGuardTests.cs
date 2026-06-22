using CodeSnifferDog.Modules.Tools.Attempts;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Tests.Modules.Tools.Attempts;

[TestClass]
public sealed class ScopedAttemptWriteGuardTests
{
    [TestMethod]
    public void CanWrite_AllowsWrites_WhenNoAttemptIsActive()
    {
        ScopedAttemptWriteGuard<string> guard = new();

        Assert.IsTrue(guard.CanWrite("scope"));
    }

    [TestMethod]
    public async Task CanWrite_AllowsWrites_WhenCurrentAttemptMatches()
    {
        ScopedAttemptWriteGuard<string> guard = new();
        Guid attemptId = Guid.NewGuid();

        guard.BeginAttempt("scope", attemptId, () => { });

        bool canWrite = await AgentRunAttemptContext.RunAsync(
            attemptId,
            () => Task.FromResult(guard.CanWrite("scope")));

        Assert.IsTrue(canWrite);
    }

    [TestMethod]
    public async Task Restore_BlocksLateWritesFromStaleAttempt()
    {
        ScopedAttemptWriteGuard<string> guard = new();
        Guid attemptId = Guid.NewGuid();
        IAgentAttemptLease lease = guard.BeginAttempt("scope", attemptId, () => { });

        lease.Restore();

        bool canWrite = await AgentRunAttemptContext.RunAsync(
            attemptId,
            () => Task.FromResult(guard.CanWrite("scope")));

        Assert.IsFalse(canWrite);
    }

    [TestMethod]
    public async Task ActiveAttempts_DoNotBlockOtherKeys()
    {
        ScopedAttemptWriteGuard<string> guard = new();
        Guid attemptId = Guid.NewGuid();

        guard.BeginAttempt("first", attemptId, () => { });

        bool canWrite = await AgentRunAttemptContext.RunAsync(
            attemptId,
            () => Task.FromResult(guard.CanWrite("second")));

        Assert.IsTrue(canWrite);
    }
}
