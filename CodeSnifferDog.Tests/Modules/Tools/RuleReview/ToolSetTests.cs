using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.RuleReview.Tools;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;

namespace CodeSnifferDog.Tests.Modules.Tools.RuleReview;

[TestClass]
public sealed class ToolSetTests
{
    private const string RuleFileName = "performance";

    [TestMethod]
    public async Task SubmitNoIssueConclusionAsync_Fails_WhenIssuesExist()
    {
        ToolSet toolSet = new(
            new InMemoryIssueStore(),
            new ReviewVerdictBuffer(),
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", RuleFileName));

        await toolSet.CreateRuleReviewIssueAsync(
            new CreateRuleReviewIssueArgs
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

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => toolSet.SubmitNoIssueConclusionAsync(
            new SubmitNoIssueConclusionArgs
            {
                ReviewStrategy = "Reviewed the current scope.",
                ScopeCoverage = "Inspected Program.cs.",
                CrossScopeAnalysis = "No cross-scope inspection was required.",
                WhyNoIssueWasFound = "No issue was found.",
            },
            TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task CreateRuleReviewIssueAsync_ResetsExistingNoIssueConclusion()
    {
        ToolSet toolSet = new(
            new InMemoryIssueStore(),
            new ReviewVerdictBuffer(),
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", RuleFileName));

        await toolSet.SubmitNoIssueConclusionAsync(
            new SubmitNoIssueConclusionArgs
            {
                ReviewStrategy = "Reviewed the current scope.",
                ScopeCoverage = "Inspected Program.cs.",
                CrossScopeAnalysis = "No cross-scope inspection was required.",
                WhyNoIssueWasFound = "No issue was found.",
            },
            TestContext.CancellationToken);

        await toolSet.CreateRuleReviewIssueAsync(
            new CreateRuleReviewIssueArgs
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

        NoIssueConclusion? noIssueConclusion = await toolSet.GetNoIssueConclusionAsync(TestContext.CancellationToken);

        Assert.IsNull(noIssueConclusion);
    }

    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task PublicMethods_DelegateToServices()
    {
        InMemoryIssueStore store = new();
        ReviewVerdictBuffer verdictBuffer = new();
        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", RuleFileName);
        ToolSet toolSet = new(store, verdictBuffer, ruleFlowKey);

        CreateRuleReviewIssueResult created = await toolSet.CreateRuleReviewIssueAsync(
            CreateIssueArgs(" high ", " Program.cs "),
            TestContext.CancellationToken);
        StoredIssue fetched = await toolSet.GetRuleReviewIssueAsync(
            new GetRuleReviewIssueArgs
            {
                RuleReviewIssueId = $" {created.RuleReviewIssueId} ",
            },
            TestContext.CancellationToken);
        IReadOnlyList<StoredIssue> issues = await toolSet.ListRuleReviewIssuesAsync(TestContext.CancellationToken);
        bool deleted = await toolSet.DeleteRuleReviewIssueAsync(
            new DeleteRuleReviewIssueArgs
            {
                RuleReviewIssueId = created.RuleReviewIssueId,
            },
            TestContext.CancellationToken);
        bool verdictSubmitted = await toolSet.SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = true,
                Message = " approved ",
            },
            TestContext.CancellationToken);

        ReviewVerdict? verdict = verdictBuffer.GetLatest(RuleScopeKeyFactory.CreateReviewVerdictScopeKey(ruleFlowKey));
        Assert.AreEqual(created.RuleReviewIssueId, fetched.RuleReviewIssueId);
        Assert.AreEqual("Program.cs", fetched.FileOrFunction);
        Assert.HasCount(1, issues);
        Assert.IsTrue(deleted);
        Assert.IsTrue(verdictSubmitted);
        Assert.IsNull(verdictBuffer.Latest);
        Assert.IsNotNull(verdict);
        Assert.AreEqual("approved", verdict.Message);
    }

    [TestMethod]
    public async Task CreateRuleReviewIssueAsync_NormalizesSeverity()
    {
        ToolSet toolSet = new(
            new InMemoryIssueStore(),
            new ReviewVerdictBuffer(),
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", RuleFileName));

        await toolSet.CreateRuleReviewIssueAsync(
            new CreateRuleReviewIssueArgs
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

        IReadOnlyList<StoredIssue> issues = await toolSet.ListRuleReviewIssuesAsync(TestContext.CancellationToken);

        Assert.HasCount(1, issues);
        Assert.AreEqual(Severity.High, issues[0].Severity);
    }

    [TestMethod]
    public async Task CreateRuleReviewIssueAsync_FailsForInvalidSeverity()
    {
        ToolSet toolSet = new(
            new InMemoryIssueStore(),
            new ReviewVerdictBuffer(),
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", RuleFileName));

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => toolSet.CreateRuleReviewIssueAsync(
            new CreateRuleReviewIssueArgs
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
    public async Task CreateRuleReviewIssueAsync_FailsForMissingRequiredField()
    {
        ToolSet toolSet = new(
            new InMemoryIssueStore(),
            new ReviewVerdictBuffer(),
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", RuleFileName));

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => toolSet.CreateRuleReviewIssueAsync(
            new CreateRuleReviewIssueArgs
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
    public async Task ListRuleReviewIssuesAsync_IsolatedByRuleFlowKey()
    {
        InMemoryIssueStore store = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ToolSet firstToolSet = new(
            store,
            verdictBuffer,
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-1", RuleFileName));
        ToolSet secondToolSet = new(
            store,
            verdictBuffer,
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", "task-2", RuleFileName));

        await firstToolSet.CreateRuleReviewIssueAsync(
            new CreateRuleReviewIssueArgs
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

        IReadOnlyList<StoredIssue> firstIssues =
            await firstToolSet.ListRuleReviewIssuesAsync(TestContext.CancellationToken);
        IReadOnlyList<StoredIssue> secondIssues =
            await secondToolSet.ListRuleReviewIssuesAsync(TestContext.CancellationToken);

        Assert.HasCount(1, firstIssues);
        Assert.IsEmpty(secondIssues);
    }

    private static CreateRuleReviewIssueArgs CreateIssueArgs(string severity, string fileOrFunction) =>
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
