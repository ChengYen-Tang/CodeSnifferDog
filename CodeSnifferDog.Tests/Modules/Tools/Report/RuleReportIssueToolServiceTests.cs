using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Report;

namespace CodeSnifferDog.Tests.Modules.Tools.Report;

[TestClass]
public sealed class RuleReportIssueToolServiceTests
{
    private const string PerformanceRuleFileName = "performance";
    private const string MemoryRuleFileName = "memory";

    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task CreateGetListUpdateDeleteIssueAsync_UsesScopedStoreAndMapsResults()
    {
        InMemoryRuleReportIssueStore store = new();
        RuleFlowKey ruleFlowKey = CreateRuleFlowKey("task-1", PerformanceRuleFileName);
        await InitializeWorkingReportAsync(store, ruleFlowKey, PerformanceRuleFileName);
        RuleReportIssueToolService service = new(store, ruleFlowKey);

        CreateRuleReportIssueResult created = await service.CreateRuleReportIssueAsync(
            CreateIssueArgs(" high ", " Program.cs "),
            TestContext.CancellationToken);
        StoredRuleReportIssue fetched = await service.GetRuleReportIssueAsync(
            new GetRuleReportIssueArgs
            {
                RuleReportIssueId = $" {created.RuleReportIssueId} ",
            },
            TestContext.CancellationToken);
        StoredRuleReportIssue updated = await service.UpdateRuleReportIssueAsync(
            new UpdateRuleReportIssueArgs
            {
                RuleReportIssueId = created.RuleReportIssueId,
                IssueType = "Performance",
                Severity = "Low",
                FileOrFunction = "Cache.cs",
                RelevantCodePatternOrExpression = "Repeated synchronous call",
                WhyThisIsAProblem = "This blocks the hot path.",
                Confidence = "Medium",
                FollowUpFiles = "Cache.cs",
                SuggestedFixDirection = "Use a cached async path.",
                ScopeCoverage = "Inspected Cache.cs.",
                CrossScopeAnalysis = "No cross-scope inspection was required.",
                ReviewStrategy = "Reviewed the hot path first.",
            },
            TestContext.CancellationToken);
        IReadOnlyList<StoredRuleReportIssue> issues = await service.ListRuleReportIssuesAsync(TestContext.CancellationToken);
        bool deleted = await service.DeleteRuleReportIssueAsync(
            new DeleteRuleReportIssueArgs
            {
                RuleReportIssueId = created.RuleReportIssueId,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(created.RuleReportIssueId, fetched.RuleReportIssueId);
        Assert.AreEqual(RuleReviewSeverity.High, fetched.Severity);
        Assert.AreEqual("Program.cs", fetched.FileOrFunction);
        Assert.AreEqual(RuleReviewSeverity.Low, updated.Severity);
        Assert.HasCount(1, issues);
        Assert.IsTrue(deleted);
    }

    [TestMethod]
    public async Task GetAndSetLatestDiffAsync_PreservesDiffReference()
    {
        RuleFlowKey ruleFlowKey = CreateRuleFlowKey("task-1", PerformanceRuleFileName);
        RuleReportIssueToolService service = new(new InMemoryRuleReportIssueStore(), ruleFlowKey);
        RuleReportDiff diff = new()
        {
            CreatedIssues = [],
            UpdatedIssues = [],
            DeletedIssues = [],
        };

        await service.SetLatestDiffAsync(diff, TestContext.CancellationToken);
        RuleReportDiff storedDiff = await service.GetLatestDiffAsync(TestContext.CancellationToken);

        Assert.AreSame(diff, storedDiff);
    }

    [TestMethod]
    public async Task ListRuleReportIssuesAsync_IsolatedByRuleAndFlow()
    {
        InMemoryRuleReportIssueStore store = new();
        RuleFlowKey firstFlowKey = CreateRuleFlowKey("task-1", PerformanceRuleFileName);
        RuleFlowKey secondFlowKey = CreateRuleFlowKey("task-2", MemoryRuleFileName);
        await InitializeWorkingReportAsync(store, firstFlowKey, PerformanceRuleFileName);
        await InitializeWorkingReportAsync(store, secondFlowKey, MemoryRuleFileName);
        RuleReportIssueToolService first = new(store, firstFlowKey);
        RuleReportIssueToolService second = new(store, secondFlowKey);

        await first.CreateRuleReportIssueAsync(CreateIssueArgs("High", "Program.cs"), TestContext.CancellationToken);

        Assert.HasCount(1, await first.ListRuleReportIssuesAsync(TestContext.CancellationToken));
        Assert.IsEmpty(await second.ListRuleReportIssuesAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CreateRuleReportIssueAsync_ThrowsForInvalidFieldsAndSeverity()
    {
        InMemoryRuleReportIssueStore store = new();
        RuleFlowKey ruleFlowKey = CreateRuleFlowKey("task-1", PerformanceRuleFileName);
        await InitializeWorkingReportAsync(store, ruleFlowKey, PerformanceRuleFileName);
        RuleReportIssueToolService service = new(store, ruleFlowKey);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.CreateRuleReportIssueAsync(CreateIssueArgs("High", " "), TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            service.CreateRuleReportIssueAsync(CreateIssueArgs("Critical", "Program.cs"), TestContext.CancellationToken).AsTask());
    }

    private async ValueTask InitializeWorkingReportAsync(
        InMemoryRuleReportIssueStore store,
        RuleFlowKey ruleFlowKey,
        string ruleFileName)
    {
        await store.InitializeWorkingReportAsync(
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", ruleFileName),
            ruleFileName,
            ruleFlowKey,
            TestContext.CancellationToken);
    }

    private static RuleFlowKey CreateRuleFlowKey(string taskItemId, string ruleFileName) =>
        RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", taskItemId, ruleFileName);

    private static CreateRuleReportIssueArgs CreateIssueArgs(string severity, string fileOrFunction) =>
        new()
        {
            IssueType = "Performance",
            Severity = severity,
            FileOrFunction = fileOrFunction,
            RelevantCodePatternOrExpression = "Repeated synchronous call",
            WhyThisIsAProblem = "This blocks the hot path.",
            Confidence = "High",
            FollowUpFiles = fileOrFunction,
            SuggestedFixDirection = "Use a cached async path.",
            ScopeCoverage = $"Inspected {fileOrFunction}.",
            CrossScopeAnalysis = "No cross-scope inspection was required.",
            ReviewStrategy = "Reviewed the hot path first.",
        };
}
