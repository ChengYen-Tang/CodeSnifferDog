using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Tests.Modules.Tools.Review;

[TestClass]
public sealed class VerdictBufferTests
{
    [TestMethod]
    public async Task BeginAttempt_Restore_BlocksLateWritesFromTimedOutAttempt()
    {
        ReviewVerdictBuffer buffer = new();
        string scopeKey = "review-scope";
        Guid attemptId = Guid.NewGuid();

        buffer.Submit(scopeKey, approved: true, message: "original");
        IAgentAttemptLease lease = buffer.BeginAttempt(scopeKey, attemptId);

        await AgentRunAttemptContext.RunAsync(attemptId, () =>
        {
            buffer.Submit(scopeKey, approved: false, message: "timed-out attempt");
            return Task.FromResult(0);
        });

        lease.Restore();

        await AgentRunAttemptContext.RunAsync(attemptId, () =>
        {
            buffer.Submit(scopeKey, approved: false, message: "late write");
            return Task.FromResult(0);
        });

        ReviewVerdict? verdict = buffer.GetLatest(scopeKey);

        Assert.IsNotNull(verdict);
        Assert.IsTrue(verdict.Approved);
        Assert.AreEqual("original", verdict.Message);
    }

    [TestMethod]
    public async Task BeginAttempt_DoesNotBlockIndependentScopes()
    {
        ReviewVerdictBuffer buffer = new();
        string firstScope = "first-scope";
        string secondScope = "second-scope";
        Guid attemptId = Guid.NewGuid();

        buffer.Submit(firstScope, approved: true, message: "first original");
        buffer.Submit(secondScope, approved: true, message: "second original");
        IAgentAttemptLease lease = buffer.BeginAttempt(firstScope, attemptId);

        await AgentRunAttemptContext.RunAsync(attemptId, () =>
        {
            buffer.Submit(firstScope, approved: false, message: "first stale");
            return Task.FromResult(0);
        });

        buffer.Submit(secondScope, approved: false, message: "second parallel");
        lease.Restore();

        Assert.AreEqual("first original", buffer.GetLatest(firstScope)!.Message);
        Assert.AreEqual("second parallel", buffer.GetLatest(secondScope)!.Message);
    }

    [TestMethod]
    public async Task BeginAttempt_Restore_BlocksLateResetFromTimedOutAttempt()
    {
        ReviewVerdictBuffer buffer = new();
        string scopeKey = "review-scope";
        Guid attemptId = Guid.NewGuid();

        buffer.Submit(scopeKey, approved: true, message: "original");
        IAgentAttemptLease lease = buffer.BeginAttempt(scopeKey, attemptId);
        lease.Restore();

        await AgentRunAttemptContext.RunAsync(attemptId, () =>
        {
            buffer.Reset(scopeKey);
            return Task.FromResult(0);
        });

        ReviewVerdict? verdict = buffer.GetLatest(scopeKey);
        Assert.IsNotNull(verdict);
        Assert.AreEqual("original", verdict.Message);
    }
}
