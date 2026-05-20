using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Tests.Modules.Tools.Report;

[TestClass]
public sealed class InMemoryRuleReportIssueStoreTests
{
    [TestMethod]
    public async Task BeginAttempt_Restore_OnlyRewindsCurrentFlow()
    {
        InMemoryRuleReportIssueStore store = new();
        RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\GitHub\CodeSnifferDog", "performance");
        RuleFlowKey firstFlow = RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "task-item-1", "performance");
        RuleFlowKey secondFlow = RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "task-item-2", "performance");

        await store.InitializeWorkingReportAsync(ruleReportKey, "performance", firstFlow, CancellationToken.None);
        await store.InitializeWorkingReportAsync(ruleReportKey, "performance", secondFlow, CancellationToken.None);
        await store.AddAsync(firstFlow, CreateIssue("Program.cs"), CancellationToken.None);
        await store.AddAsync(secondFlow, CreateIssue("Cache.cs"), CancellationToken.None);

        Guid attemptId = Guid.NewGuid();
        IAgentAttemptLease lease = store.BeginAttempt(firstFlow, attemptId);

        await AgentRunAttemptContext.RunAsync(attemptId, async () =>
        {
            await store.AddAsync(firstFlow, CreateIssue("Stale.cs"), CancellationToken.None);
            await store.SetLatestDiffAsync(
                firstFlow,
                new RuleReportDiff
                {
                    CreatedIssues = [],
                    UpdatedIssues = [new StoredRuleReportIssue { RuleReportIssueId = "stale", IssueType = "Performance", Severity = "High", FileOrFunction = "Stale.cs", RelevantCodePatternOrExpression = "call", WhyThisIsAProblem = "problem", Confidence = "High", FollowUpFiles = "Stale.cs", SuggestedFixDirection = "fix", ReviewStrategy = "strategy", ScopeCoverage = "scope", CrossScopeAnalysis = "cross" }],
                    DeletedIssues = [],
                },
                CancellationToken.None);
            return 0;
        });

        await store.AddAsync(secondFlow, CreateIssue("LateParallel.cs"), CancellationToken.None);
        lease.Restore();

        IReadOnlyList<StoredRuleReportIssue> firstFlowIssues = await store.ListAsync(firstFlow, CancellationToken.None);
        IReadOnlyList<StoredRuleReportIssue> secondFlowIssues = await store.ListAsync(secondFlow, CancellationToken.None);
        RuleReportDiff firstFlowDiff = await store.GetLatestDiffAsync(firstFlow, CancellationToken.None);

        Assert.HasCount(1, firstFlowIssues);
        Assert.AreEqual("Program.cs", firstFlowIssues[0].FileOrFunction);
        Assert.IsEmpty(firstFlowDiff.CreatedIssues);
        Assert.IsEmpty(firstFlowDiff.UpdatedIssues);
        Assert.IsEmpty(firstFlowDiff.DeletedIssues);
        Assert.HasCount(2, secondFlowIssues);
        Assert.IsTrue(secondFlowIssues.Any(issue => issue.FileOrFunction == "Cache.cs"));
        Assert.IsTrue(secondFlowIssues.Any(issue => issue.FileOrFunction == "LateParallel.cs"));
    }

    [TestMethod]
    public async Task BeginAttempt_Restore_BlocksLateWritesFromTimedOutAttempt()
    {
        InMemoryRuleReportIssueStore store = new();
        RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\GitHub\CodeSnifferDog", "performance");
        RuleFlowKey flow = RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "task-item-1", "performance");
        Guid attemptId = Guid.NewGuid();

        await store.InitializeWorkingReportAsync(ruleReportKey, "performance", flow, CancellationToken.None);
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

        IReadOnlyList<StoredRuleReportIssue> issues = await store.ListAsync(flow, CancellationToken.None);

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
