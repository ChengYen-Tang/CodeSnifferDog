using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Tests.Modules.Tools.RuleReview;

[TestClass]
public sealed class InMemoryRuleReviewIssueStoreTests
{
    [TestMethod]
    public async Task BeginAttempt_Restore_OnlyRewindsCurrentFlow()
    {
        InMemoryRuleReviewIssueStore store = new();
        RuleFlowKey firstFlow = RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "task-item-1", "performance");
        RuleFlowKey secondFlow = RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "task-item-2", "memory");

        await store.AddAsync(firstFlow, CreateIssue("Program.cs"), CancellationToken.None);
        await store.AddAsync(secondFlow, CreateIssue("Cache.cs"), CancellationToken.None);

        Guid attemptId = Guid.NewGuid();
        IAgentAttemptLease lease = store.BeginAttempt(firstFlow, attemptId);

        await AgentRunAttemptContext.RunAsync(attemptId, async () =>
        {
            await store.AddAsync(firstFlow, CreateIssue("Stale.cs"), CancellationToken.None);
            return 0;
        });

        await store.AddAsync(secondFlow, CreateIssue("LateParallel.cs"), CancellationToken.None);
        lease.Restore();

        IReadOnlyList<StoredRuleReviewIssue> firstFlowIssues = await store.ListAsync(firstFlow, CancellationToken.None);
        IReadOnlyList<StoredRuleReviewIssue> secondFlowIssues = await store.ListAsync(secondFlow, CancellationToken.None);

        Assert.HasCount(1, firstFlowIssues);
        Assert.AreEqual("Program.cs", firstFlowIssues[0].FileOrFunction);
        Assert.HasCount(2, secondFlowIssues);
        Assert.IsTrue(secondFlowIssues.Any(issue => issue.FileOrFunction == "Cache.cs"));
        Assert.IsTrue(secondFlowIssues.Any(issue => issue.FileOrFunction == "LateParallel.cs"));
    }

    [TestMethod]
    public async Task BeginAttempt_Restore_BlocksLateWritesFromTimedOutAttempt()
    {
        InMemoryRuleReviewIssueStore store = new();
        RuleFlowKey flow = RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "task-item-1", "performance");
        Guid attemptId = Guid.NewGuid();

        await store.AddAsync(flow, CreateIssue("Program.cs"), CancellationToken.None);
        IAgentAttemptLease lease = store.BeginAttempt(flow, attemptId);

        await AgentRunAttemptContext.RunAsync(attemptId, async () =>
        {
            await store.AddAsync(flow, CreateIssue("TimedOut.cs"), CancellationToken.None);
            return 0;
        });

        lease.Restore();

        await AgentRunAttemptContext.RunAsync(attemptId, async () =>
        {
            await store.AddAsync(flow, CreateIssue("LateWrite.cs"), CancellationToken.None);
            return 0;
        });

        IReadOnlyList<StoredRuleReviewIssue> issues = await store.ListAsync(flow, CancellationToken.None);

        Assert.HasCount(1, issues);
        Assert.AreEqual("Program.cs", issues[0].FileOrFunction);
    }

    private static RuleReviewIssue CreateIssue(string fileOrFunction) =>
        new()
        {
            IssueType = "Performance",
            Severity = "High",
            FileOrFunction = fileOrFunction,
            RelevantCodePatternOrExpression = "Repeated synchronous call",
            WhyThisIsAProblem = "This blocks the request path.",
            Confidence = "High",
            FollowUpFiles = fileOrFunction,
            SuggestedFixDirection = "Use a cached async path.",
            ReviewStrategy = "Reviewed the hot path first.",
            ScopeCoverage = $"Inspected {fileOrFunction}.",
            CrossScopeAnalysis = "No cross-scope inspection was required.",
        };
}
