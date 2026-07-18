using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Workflows.Common;
using StoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Tests.Modules.Tools.Report;

[TestClass]
public sealed class InMemoryIssueStoreTests
{
    [TestMethod]
    public async Task BeginAttempt_Restore_OnlyRewindsCurrentFlow()
    {
        InMemoryIssueStore store = new();
        RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(TestRepositoryPaths.RootPath, "performance");
        RuleFlowKey firstFlow = RuleScopeKeyFactory.CreateRuleFlowKey(TestRepositoryPaths.RootPath, "task-item-1", "performance");
        RuleFlowKey secondFlow = RuleScopeKeyFactory.CreateRuleFlowKey(TestRepositoryPaths.RootPath, "task-item-2", "performance");

        await store.InitializeWorkingReportAsync(ruleReportKey, "performance", firstFlow, CancellationToken.None);
        await store.InitializeWorkingReportAsync(ruleReportKey, "performance", secondFlow, CancellationToken.None);
        await store.AddAsync(firstFlow, CreateIssue("Program.cs"), CancellationToken.None);
        await store.AddAsync(secondFlow, CreateIssue("Cache.cs"), CancellationToken.None);

        Guid attemptId = Guid.CreateVersion7();
        IAgentAttemptLease lease = store.BeginAttempt(firstFlow, attemptId);

        await AgentRunAttemptContext.RunAsync(attemptId, async () =>
        {
            await store.AddAsync(firstFlow, CreateIssue("Stale.cs"), CancellationToken.None);
            await store.SetLatestDiffAsync(
                firstFlow,
                new Diff
                {
                    CreatedIssues = [],
                    UpdatedIssues = [new StoredIssue { RuleReportIssueId = "stale", IssueType = "Performance", Severity = "High", FileOrFunction = "Stale.cs", RelevantCodePatternOrExpression = "call", WhyThisIsAProblem = "problem", Confidence = "High", FollowUpFiles = "Stale.cs", SuggestedFixDirection = "fix", ReviewStrategy = "strategy", ScopeCoverage = "scope", CrossScopeAnalysis = "cross" }],
                    DeletedIssues = [],
                },
                CancellationToken.None);
            return 0;
        });

        await store.AddAsync(secondFlow, CreateIssue("LateParallel.cs"), CancellationToken.None);
        lease.Restore();

        IReadOnlyList<StoredIssue> firstFlowIssues = await store.ListAsync(firstFlow, CancellationToken.None);
        IReadOnlyList<StoredIssue> secondFlowIssues = await store.ListAsync(secondFlow, CancellationToken.None);
        Diff firstFlowDiff = await store.GetLatestDiffAsync(firstFlow, CancellationToken.None);

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
        InMemoryIssueStore store = new();
        RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(TestRepositoryPaths.RootPath, "performance");
        RuleFlowKey flow = RuleScopeKeyFactory.CreateRuleFlowKey(TestRepositoryPaths.RootPath, "task-item-1", "performance");
        Guid attemptId = Guid.CreateVersion7();

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

        IReadOnlyList<StoredIssue> issues = await store.ListAsync(flow, CancellationToken.None);

        Assert.HasCount(1, issues);
        Assert.AreEqual("Program.cs", issues[0].FileOrFunction);
    }

    private static Issue CreateIssue(string fileOrFunction) =>
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
