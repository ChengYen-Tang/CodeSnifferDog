using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;
using CodeSnifferDog.Modules.Tools.RuleReview.State;

namespace CodeSnifferDog.Tests.Modules.Tools.RuleReview.State;

[TestClass]
public sealed class IssueStateStoreTests
{
    [TestMethod]
    public void Add_DeduplicatesNormalizedIssuesAndClearsNoIssueConclusion()
    {
        IssueStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");
        store.SubmitNoIssueConclusion(flow, CreateConclusion());

        StoredIssue first = store.Add(flow, CreateIssue(" Program.cs "), "first");
        StoredIssue second = store.Add(flow, CreateIssue("Program.cs"), "second");

        Assert.AreSame(first, second);
        Assert.IsNull(store.GetNoIssueConclusion(flow));
        Assert.HasCount(1, store.ListPage(flow, null, 20));
    }

    [TestMethod]
    public void UpdateDeleteAndList_MutateCurrentFlowOnly()
    {
        IssueStateStore store = new();
        RuleFlowKey firstFlow = CreateFlow("task-1");
        RuleFlowKey secondFlow = CreateFlow("task-2");

        StoredIssue first = store.Add(firstFlow, CreateIssue("Program.cs"), "first");
        store.Add(secondFlow, CreateIssue("Cache.cs"), "second");

        StoredIssue updated = store.Update(firstFlow, first.RuleReviewIssueId, CreateIssue("Updated.cs"));
        bool deleted = store.Delete(firstFlow, updated.RuleReviewIssueId);

        Assert.AreEqual("Updated.cs", updated.FileOrFunction);
        Assert.IsTrue(deleted);
        Assert.IsEmpty(store.ListPage(firstFlow, null, 20));
        Assert.HasCount(1, store.ListPage(secondFlow, null, 20));
    }

    [TestMethod]
    public void CloneRestore_RewindsState()
    {
        IssueStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");
        store.Add(flow, CreateIssue("Program.cs"), "first");
        FlowState? snapshot = store.Clone(flow);

        store.Add(flow, CreateIssue("Stale.cs"), "second");
        store.Restore(flow, snapshot);

        IReadOnlyList<StoredIssue> issues = store.ListPage(flow, null, 20);
        Assert.HasCount(1, issues);
        Assert.AreEqual("Program.cs", issues[0].FileOrFunction);
    }

    [TestMethod]
    public void SubmitNoIssueConclusion_Throws_WhenIssuesExist()
    {
        IssueStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");
        store.Add(flow, CreateIssue("Program.cs"), "first");

        Assert.ThrowsExactly<InvalidOperationException>(() => store.SubmitNoIssueConclusion(flow, CreateConclusion()));
    }

    [TestMethod]
    public void ListPage_ContinuesAfterDeletedCursor()
    {
        IssueStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");
        store.Add(flow, CreateIssue("First.cs"), "0001");
        StoredIssue second = store.Add(flow, CreateIssue("Second.cs"), "0002");
        store.Add(flow, CreateIssue("Third.cs"), "0003");

        IReadOnlyList<StoredIssue> firstPage = store.ListPage(flow, null, 2);
        store.Delete(flow, second.RuleReviewIssueId);
        IReadOnlyList<StoredIssue> nextPage = store.ListPage(flow, second.RuleReviewIssueId, 2);

        CollectionAssert.AreEqual(new[] { "0001", "0002" }, firstPage.Select(issue => issue.RuleReviewIssueId).ToArray());
        CollectionAssert.AreEqual(new[] { "0003" }, nextPage.Select(issue => issue.RuleReviewIssueId).ToArray());
    }

    private static NormalizedRuleIssue CreateIssue(string fileOrFunction) =>
        RuleIssueNormalizer.NormalizeToContract(new Issue
        {
            IssueType = "Performance",
            Severity = "High",
            FileOrFunction = fileOrFunction,
            RelevantCodePatternOrExpression = "call",
            WhyThisIsAProblem = "problem",
            Confidence = "High",
            FollowUpFiles = fileOrFunction,
            SuggestedFixDirection = "fix",
            ReviewStrategy = "strategy",
            ScopeCoverage = "scope",
            CrossScopeAnalysis = "cross",
        });

    private static NoIssueConclusion CreateConclusion() =>
        new()
        {
            ReviewStrategy = "strategy",
            ScopeCoverage = "scope",
            CrossScopeAnalysis = "cross",
            WhyNoIssueWasFound = "reason",
        };

    private static RuleFlowKey CreateFlow(string taskItemId) =>
        RuleScopeKeyFactory.CreateRuleFlowKey(TestRepositoryPaths.RootPath, taskItemId, "performance");
}
