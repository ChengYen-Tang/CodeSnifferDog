using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Tests.Modules.Tools.Review;

[TestClass]
public sealed class ReviewVerdictBufferTests
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
}
