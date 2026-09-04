using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;
using CodeSnifferDog.Models.Report.Tools.Listing;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.Text.Json;
using StoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Tests.Modules.Tools.Report;

[TestClass]
public sealed class ToolSetTests
{
    private const string PerformanceRuleFileName = "performance";
    private const string MemoryRuleFileName = "memory";

    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task PublicMethods_DelegateToServices()
    {
        InMemoryIssueStore store = new();
        ReviewVerdictBuffer verdictBuffer = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ToolSet toolSet = new(store, verdictBuffer, ruleFlowKey, ruleReportKey);

        CreateRuleReportIssueResult created = await toolSet.CreateRuleReportIssueAsync(
            CreateIssueArgs(" Program.cs ", "Repeated synchronous call", "Use a cached async path."),
            TestContext.CancellationToken);
        StoredIssue fetched = await toolSet.GetRuleReportIssueAsync(
            new GetRuleReportIssueArgs
            {
                RuleReportIssueId = $" {created.RuleReportIssueId} ",
            },
            TestContext.CancellationToken);
        IssuePage issues = await toolSet.ListRuleReportIssuesAsync(new ListIssuesArgs(), TestContext.CancellationToken);
        bool deleted = await toolSet.DeleteRuleReportIssueAsync(
            new DeleteRuleReportIssueArgs
            {
                RuleReportIssueId = created.RuleReportIssueId,
            },
            TestContext.CancellationToken);
        bool verdictSubmitted = await toolSet.SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = true,
                Message = " approved ",
            },
            TestContext.CancellationToken);

        ReviewVerdict? verdict = verdictBuffer.GetLatest(RuleScopeKeyFactory.CreateReportVerdictScopeKey(ruleFlowKey));
        Assert.AreEqual(created.RuleReportIssueId, fetched.RuleReportIssueId);
        Assert.AreEqual("Program.cs", fetched.FileOrFunction);
        Assert.HasCount(1, issues.Items);
        Assert.IsTrue(deleted);
        Assert.IsTrue(verdictSubmitted);
        Assert.IsNull(verdictBuffer.Latest);
        Assert.IsNotNull(verdict);
        Assert.AreEqual("approved", verdict.Message);
    }

    [TestMethod]
    public async Task CreateRuleReportIssueAsync_ReturnsGeneratedId()
    {
        InMemoryIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);

        CreateRuleReportIssueResult result = await toolSet.CreateRuleReportIssueAsync(
            new CreateRuleReportIssueArgs
            {
                IssueType = "Performance",
                Severity = "High",
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
    public async Task CreateRuleReportIssueAsync_NormalizesSeverity()
    {
        InMemoryIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);

        await toolSet.CreateRuleReportIssueAsync(
            new CreateRuleReportIssueArgs
            {
                IssueType = "Performance",
                Severity = " high ",
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

        IssuePage issues = await toolSet.ListRuleReportIssuesAsync(new ListIssuesArgs(), TestContext.CancellationToken);

        Assert.HasCount(1, issues.Items);
        Assert.AreEqual(Severity.High, issues.Items[0].Severity);
    }

    [TestMethod]
    public async Task CreateRuleReportIssueAsync_FailsForInvalidSeverity()
    {
        InMemoryIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => toolSet.CreateRuleReportIssueAsync(
            new CreateRuleReportIssueArgs
            {
                IssueType = "Performance",
                Severity = "Critical",
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
            TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task CreateRuleReportIssueAsync_FailsForMissingRequiredField()
    {
        InMemoryIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => toolSet.CreateRuleReportIssueAsync(
            new CreateRuleReportIssueArgs
            {
                IssueType = "Performance",
                Severity = "High",
                FileOrFunction = " ",
                RelevantCodePatternOrExpression = "Repeated synchronous call",
                WhyThisIsAProblem = "This blocks the hot path.",
                Confidence = "High",
                FollowUpFiles = "Program.cs",
                SuggestedFixDirection = "Use a cached async path.",
                ScopeCoverage = "Inspected Program.cs.",
                CrossScopeAnalysis = "No cross-scope inspection was required.",
                ReviewStrategy = "Reviewed the hot path first.",
            },
            TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task PromoteWorkingReportAsync_PreservesSnapshotForNextFlow()
    {
        InMemoryIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);

        CreateRuleReportIssueResult createdIssue = await toolSet.CreateRuleReportIssueAsync(
            CreateIssueArgs("Program.cs", "Repeated synchronous call", "Use a cached async path."),
            TestContext.CancellationToken);

        await store.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, TestContext.CancellationToken);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);

        IssuePage workingIssues = await toolSet.ListRuleReportIssuesAsync(new ListIssuesArgs(), TestContext.CancellationToken);

        Assert.HasCount(1, workingIssues.Items);
        Assert.AreEqual(createdIssue.RuleReportIssueId, workingIssues.Items[0].RuleReportIssueId);
    }

    [TestMethod]
    public async Task SetLatestDiffAsync_PreservesDiffForVerifier()
    {
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        ToolSet toolSet = new(new InMemoryIssueStore(), new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);
        Diff diff = new()
        {
            CreatedIssues = [],
            UpdatedIssues = [],
            DeletedIssues = [],
        };

        await toolSet.SetLatestDiffAsync(diff, TestContext.CancellationToken);

        Diff storedDiff = await toolSet.GetLatestDiffAsync(TestContext.CancellationToken);

        Assert.AreSame(diff, storedDiff);
    }

    [TestMethod]
    public async Task ListRuleReportIssuesAsync_IsolatedByRuleReportKey()
    {
        InMemoryIssueStore store = new();
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
        ToolSet firstToolSet = new(store, verdictBuffer, firstFlowKey, firstReportKey);
        ToolSet secondToolSet = new(store, verdictBuffer, secondFlowKey, secondReportKey);

        await firstToolSet.CreateRuleReportIssueAsync(
            CreateIssueArgs("Program.cs", "Repeated synchronous call", "Use a cached async path."),
            TestContext.CancellationToken);

        IssuePage firstIssues = await firstToolSet.ListRuleReportIssuesAsync(new ListIssuesArgs(), TestContext.CancellationToken);
        IssuePage secondIssues = await secondToolSet.ListRuleReportIssuesAsync(new ListIssuesArgs(), TestContext.CancellationToken);

        Assert.HasCount(1, firstIssues.Items);
        Assert.IsEmpty(secondIssues.Items);
    }

    [TestMethod]
    public async Task ListRuleReportIssuesAsync_IsolatedByFlowKey_ForSameRule()
    {
        InMemoryIssueStore store = new();
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReportKey reportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        RuleFlowKey firstFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleFlowKey secondFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-2", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(reportKey, PerformanceRuleFileName, firstFlowKey, TestContext.CancellationToken);
        await store.InitializeWorkingReportAsync(reportKey, PerformanceRuleFileName, secondFlowKey, TestContext.CancellationToken);
        ToolSet firstToolSet = new(store, verdictBuffer, firstFlowKey, reportKey);
        ToolSet secondToolSet = new(store, verdictBuffer, secondFlowKey, reportKey);

        await firstToolSet.CreateRuleReportIssueAsync(
            CreateIssueArgs("Program.cs", "Repeated synchronous call", "Use a cached async path."),
            TestContext.CancellationToken);

        IssuePage firstIssues = await firstToolSet.ListRuleReportIssuesAsync(new ListIssuesArgs(), TestContext.CancellationToken);
        IssuePage secondIssues = await secondToolSet.ListRuleReportIssuesAsync(new ListIssuesArgs(), TestContext.CancellationToken);

        Assert.HasCount(1, firstIssues.Items);
        Assert.IsEmpty(secondIssues.Items);
    }

    [TestMethod]
    public async Task ListRuleReportIssuesAsync_ReturnsBoundedIndexesAndContinuation()
    {
        InMemoryIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);

        for (int index = 0; index < 11; index++)
        {
            await toolSet.CreateRuleReportIssueAsync(
                CreateIssueArgs($"File{index:D2}.cs", $"Pattern {index}", $"Fix {index}"),
                TestContext.CancellationToken);
        }

        IssuePage firstPage = await toolSet.ListRuleReportIssuesAsync(
            new ListIssuesArgs(),
            TestContext.CancellationToken);
        IssuePage secondPage = await toolSet.ListRuleReportIssuesAsync(
            new ListIssuesArgs
            {
                Cursor = firstPage.NextCursor,
            },
            TestContext.CancellationToken);

        Assert.HasCount(IssuePage.DefaultPageSize, firstPage.Items);
        Assert.IsTrue(firstPage.HasMore);
        Assert.IsNotNull(firstPage.NextCursor);
        Assert.HasCount(1, secondPage.Items);
        Assert.IsFalse(secondPage.HasMore);
        Assert.IsNull(secondPage.NextCursor);
    }

    [TestMethod]
    public async Task ListRuleReportIssuesAsync_ReturnsBoundedPreviewsAndLeavesDetailsToGet()
    {
        InMemoryIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);
        string issueType = new('T', 300);
        string location = new('L', 300);

        CreateRuleReportIssueResult created = await toolSet.CreateRuleReportIssueAsync(
            CreateIssueArgs(location, "Pattern", "Fix", issueType),
            TestContext.CancellationToken);
        IssuePage page = await toolSet.ListRuleReportIssuesAsync(new ListIssuesArgs(), TestContext.CancellationToken);
        StoredIssue detail = await toolSet.GetRuleReportIssueAsync(
            new GetRuleReportIssueArgs
            {
                RuleReportIssueId = created.RuleReportIssueId,
            },
            TestContext.CancellationToken);

        IssueListItem item = page.Items[0];
        Assert.AreEqual(120, item.IssueTypePreview.Length);
        Assert.AreEqual(160, item.LocationPreview.Length);
        Assert.IsTrue(item.IssueTypePreview.EndsWith('…'));
        Assert.IsTrue(item.LocationPreview.EndsWith('…'));
        Assert.AreEqual(issueType, detail.IssueType);
        Assert.AreEqual(location, detail.FileOrFunction);
    }

    [TestMethod]
    public async Task ListRuleReportIssuesAsync_RejectsPageSizesAboveTheBound()
    {
        InMemoryIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => toolSet.ListRuleReportIssuesAsync(
            new ListIssuesArgs
            {
                PageSize = IssuePage.MaxPageSize + 1,
            },
            TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ListRuleReportIssuesToolAsync_UsesPagedIndexContract()
    {
        InMemoryIssueStore store = new();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", PerformanceRuleFileName);
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\RepoA", PerformanceRuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, PerformanceRuleFileName, ruleFlowKey, TestContext.CancellationToken);
        ToolSet toolSet = new(store, new ReviewVerdictBuffer(), ruleFlowKey, ruleReportKey);

        await toolSet.CreateRuleReportIssueAsync(
            CreateIssueArgs("Program.cs", "Pattern", "Fix"),
            TestContext.CancellationToken);
        AIFunction tool = Assert.IsInstanceOfType<AIFunction>(
            toolSet.CreateReportAggregatorTools().Single(candidate => candidate.Name == "ListRuleReportIssues"));

        JsonElement result = Assert.IsInstanceOfType<JsonElement>(await tool.InvokeAsync(
            new AIFunctionArguments(),
            TestContext.CancellationToken));
        JsonElement item = result.GetProperty("items")[0];

        Assert.IsTrue(item.TryGetProperty("ruleReportIssueId", out _));
        Assert.IsTrue(item.TryGetProperty("severity", out _));
        Assert.IsTrue(item.TryGetProperty("issueTypePreview", out _));
        Assert.IsTrue(item.TryGetProperty("locationPreview", out _));
        Assert.IsFalse(item.TryGetProperty("whyThisIsAProblem", out _));
    }

    private static CreateRuleReportIssueArgs CreateIssueArgs(
        string fileOrFunction,
        string relevantCodePatternOrExpression,
        string suggestedFixDirection,
        string issueType = "Performance") =>
        new()
        {
            IssueType = issueType,
            Severity = "High",
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
