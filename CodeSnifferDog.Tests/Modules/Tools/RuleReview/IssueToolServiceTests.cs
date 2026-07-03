using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.RuleReview.Tools;
using CodeSnifferDog.Modules.Tools.RuleReview;

namespace CodeSnifferDog.Tests.Modules.Tools.RuleReview;

[TestClass]
public sealed class IssueToolServiceTests
{
    private const string RuleFileName = "performance";

    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task CreateGetListUpdateDeleteIssueAsync_UsesScopedStoreAndMapsResults()
    {
        RuleFlowKey ruleFlowKey = CreateRuleFlowKey("task-1");
        IssueToolService service = new(new InMemoryIssueStore(), ruleFlowKey);

        CreateRuleReviewIssueResult created = await service.CreateRuleReviewIssueAsync(
            CreateIssueArgs(" high ", " Program.cs "),
            TestContext.CancellationToken);
        StoredIssue fetched = await service.GetRuleReviewIssueAsync(
            new GetRuleReviewIssueArgs
            {
                RuleReviewIssueId = $" {created.RuleReviewIssueId} ",
            },
            TestContext.CancellationToken);
        StoredIssue updated = await service.UpdateRuleReviewIssueAsync(
            new UpdateRuleReviewIssueArgs
            {
                RuleReviewIssueId = created.RuleReviewIssueId,
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
        IReadOnlyList<StoredIssue> issues = await service.ListRuleReviewIssuesAsync(TestContext.CancellationToken);
        bool deleted = await service.DeleteRuleReviewIssueAsync(
            new DeleteRuleReviewIssueArgs
            {
                RuleReviewIssueId = created.RuleReviewIssueId,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(created.RuleReviewIssueId, fetched.RuleReviewIssueId);
        Assert.AreEqual(Severity.High, fetched.Severity);
        Assert.AreEqual("Program.cs", fetched.FileOrFunction);
        Assert.AreEqual(Severity.Low, updated.Severity);
        Assert.HasCount(1, issues);
        Assert.IsTrue(deleted);
    }

    [TestMethod]
    public async Task SubmitNoIssueConclusionAsync_StoresConclusionAndCreateIssueResetsIt()
    {
        IssueToolService service = new(
            new InMemoryIssueStore(),
            CreateRuleFlowKey("task-1"));

        await service.SubmitNoIssueConclusionAsync(
            new SubmitNoIssueConclusionArgs
            {
                ReviewStrategy = "Reviewed current scope.",
                ScopeCoverage = "Inspected Program.cs.",
                CrossScopeAnalysis = "No cross-scope inspection was required.",
                WhyNoIssueWasFound = "No issue was found.",
            },
            TestContext.CancellationToken);
        NoIssueConclusion? submitted = await service.GetNoIssueConclusionAsync(TestContext.CancellationToken);

        await service.CreateRuleReviewIssueAsync(CreateIssueArgs("High", "Program.cs"), TestContext.CancellationToken);
        NoIssueConclusion? afterIssue = await service.GetNoIssueConclusionAsync(TestContext.CancellationToken);

        Assert.IsNotNull(submitted);
        Assert.AreEqual("No issue was found.", submitted.WhyNoIssueWasFound);
        Assert.IsNull(afterIssue);
    }

    [TestMethod]
    public async Task ListRuleReviewIssuesAsync_IsolatedByRuleFlowKey()
    {
        InMemoryIssueStore store = new();
        IssueToolService first = new(store, CreateRuleFlowKey("task-1"));
        IssueToolService second = new(store, CreateRuleFlowKey("task-2"));

        await first.CreateRuleReviewIssueAsync(CreateIssueArgs("High", "Program.cs"), TestContext.CancellationToken);

        Assert.HasCount(1, await first.ListRuleReviewIssuesAsync(TestContext.CancellationToken));
        Assert.IsEmpty(await second.ListRuleReviewIssuesAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CreateRuleReviewIssueAsync_ThrowsForInvalidFieldsAndSeverity()
    {
        IssueToolService service = new(
            new InMemoryIssueStore(),
            CreateRuleFlowKey("task-1"));

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.CreateRuleReviewIssueAsync(CreateIssueArgs("High", " "), TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            service.CreateRuleReviewIssueAsync(CreateIssueArgs("Critical", "Program.cs"), TestContext.CancellationToken).AsTask());
    }

    private static RuleFlowKey CreateRuleFlowKey(string taskItemId) =>
        RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\RepoA", taskItemId, RuleFileName);

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
