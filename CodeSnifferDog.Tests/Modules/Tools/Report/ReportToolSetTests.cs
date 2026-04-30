using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;

namespace CodeSnifferDog.Tests.Modules.Tools.Report;

[TestClass]
public sealed class ReportToolSetTests
{
    private const string PerformanceRuleFileName = "performance";
    private const string MemoryRuleFileName = "memory";

    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task CreateRuleReportIssueAsync_ReturnsGeneratedId()
    {
        InMemoryRuleReportIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ReportToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);

        CreateRuleReportIssueResult result = await toolSet.CreateRuleReportIssueAsync(
            new CreateRuleReportIssueArgs
            {
                IssueType = "Performance",
                FileOrFunction = "Program.cs",
                RelevantCodePatternOrExpression = "Repeated synchronous call",
                WhyThisIsAProblem = "This blocks the hot path.",
                Confidence = "High",
                FollowUpFiles = "Program.cs",
                SuggestedFixDirection = "Use a cached async path.",
                ScopeCoverage = "Inspected Program.cs.",
                CrossScopeAnalysis = "No cross-scope inspection was required.",
                ReviewStrategy = "Reviewed the hot path first.",
            },
            TestContext.CancellationToken);

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.RuleReportIssueId));
    }

    [TestMethod]
    public async Task PromoteWorkingReportAsync_PreservesSnapshotForNextFlow()
    {
        InMemoryRuleReportIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ReportToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);

        CreateRuleReportIssueResult createdIssue = await toolSet.CreateRuleReportIssueAsync(
            CreateIssueArgs("Program.cs", "Repeated synchronous call", "Use a cached async path."),
            TestContext.CancellationToken);

        await store.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, TestContext.CancellationToken);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);

        IReadOnlyList<StoredRuleReportIssue> workingIssues = await toolSet.ListRuleReportIssuesAsync(TestContext.CancellationToken);

        Assert.HasCount(1, workingIssues);
        Assert.AreEqual(createdIssue.RuleReportIssueId, workingIssues[0].RuleReportIssueId);
    }

    [TestMethod]
    public async Task SetLatestDiffAsync_PreservesDiffForVerifier()
    {
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        ReportToolSet toolSet = new(new InMemoryRuleReportIssueStore(), new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);
        RuleReportDiff diff = new()
        {
            CreatedIssues = [],
            UpdatedIssues = [],
            DeletedIssues = [],
        };

        await toolSet.SetLatestDiffAsync(diff, TestContext.CancellationToken);

        RuleReportDiff storedDiff = await toolSet.GetLatestDiffAsync(TestContext.CancellationToken);

        Assert.AreSame(diff, storedDiff);
    }

    [TestMethod]
    public async Task ListRuleReportIssuesAsync_IsolatedByRuleReportKey()
    {
        InMemoryRuleReportIssueStore store = new();
        ReviewVerdictBuffer verdictBuffer = new();
        RuleFlowKey firstFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey firstReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        RuleFlowKey secondFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-2", MemoryRuleFileName);
        RuleReportKey secondReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", MemoryRuleFileName);
        await store.InitializeWorkingReportAsync(firstReportKey, PerformanceRuleFileName, firstFlowKey, TestContext.CancellationToken);
        await store.InitializeWorkingReportAsync(secondReportKey, MemoryRuleFileName, secondFlowKey, TestContext.CancellationToken);
        ReportToolSet firstToolSet = new(store, verdictBuffer, firstFlowKey, firstReportKey);
        ReportToolSet secondToolSet = new(store, verdictBuffer, secondFlowKey, secondReportKey);

        await firstToolSet.CreateRuleReportIssueAsync(
            CreateIssueArgs("Program.cs", "Repeated synchronous call", "Use a cached async path."),
            TestContext.CancellationToken);

        IReadOnlyList<StoredRuleReportIssue> firstIssues = await firstToolSet.ListRuleReportIssuesAsync(TestContext.CancellationToken);
        IReadOnlyList<StoredRuleReportIssue> secondIssues = await secondToolSet.ListRuleReportIssuesAsync(TestContext.CancellationToken);

        Assert.HasCount(1, firstIssues);
        Assert.IsEmpty(secondIssues);
    }

    [TestMethod]
    public async Task ListRuleReportIssuesAsync_IsolatedByFlowKey_ForSameRule()
    {
        InMemoryRuleReportIssueStore store = new();
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReportKey reportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        RuleFlowKey firstFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleFlowKey secondFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-2", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(reportKey, PerformanceRuleFileName, firstFlowKey, TestContext.CancellationToken);
        await store.InitializeWorkingReportAsync(reportKey, PerformanceRuleFileName, secondFlowKey, TestContext.CancellationToken);
        ReportToolSet firstToolSet = new(store, verdictBuffer, firstFlowKey, reportKey);
        ReportToolSet secondToolSet = new(store, verdictBuffer, secondFlowKey, reportKey);

        await firstToolSet.CreateRuleReportIssueAsync(
            CreateIssueArgs("Program.cs", "Repeated synchronous call", "Use a cached async path."),
            TestContext.CancellationToken);

        IReadOnlyList<StoredRuleReportIssue> firstIssues = await firstToolSet.ListRuleReportIssuesAsync(TestContext.CancellationToken);
        IReadOnlyList<StoredRuleReportIssue> secondIssues = await secondToolSet.ListRuleReportIssuesAsync(TestContext.CancellationToken);

        Assert.HasCount(1, firstIssues);
        Assert.IsEmpty(secondIssues);
    }

    private static CreateRuleReportIssueArgs CreateIssueArgs(
        string fileOrFunction,
        string relevantCodePatternOrExpression,
        string suggestedFixDirection) =>
        new()
        {
            IssueType = "Performance",
            FileOrFunction = fileOrFunction,
            RelevantCodePatternOrExpression = relevantCodePatternOrExpression,
            WhyThisIsAProblem = "This blocks the hot path.",
            Confidence = "High",
            FollowUpFiles = fileOrFunction,
            SuggestedFixDirection = suggestedFixDirection,
            ScopeCoverage = $"Inspected {fileOrFunction}.",
            CrossScopeAnalysis = "No cross-scope inspection was required.",
            ReviewStrategy = "Reviewed the hot path first.",
        };
}
