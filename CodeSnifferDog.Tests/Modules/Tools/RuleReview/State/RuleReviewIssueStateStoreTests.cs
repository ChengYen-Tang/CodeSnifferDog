using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;
using CodeSnifferDog.Modules.Tools.RuleReview.State;

namespace CodeSnifferDog.Tests.Modules.Tools.RuleReview.State;

[TestClass]
public sealed class RuleReviewIssueStateStoreTests
{
    [TestMethod]
    public void Add_DeduplicatesNormalizedIssuesAndClearsNoIssueConclusion()
    {
        RuleReviewIssueStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");
        store.SubmitNoIssueConclusion(flow, CreateConclusion());

        StoredRuleReviewIssue first = store.Add(flow, CreateIssue(" Program.cs "), "first");
        StoredRuleReviewIssue second = store.Add(flow, CreateIssue("Program.cs"), "second");

        Assert.AreSame(first, second);
        Assert.IsNull(store.GetNoIssueConclusion(flow));
        Assert.HasCount(1, store.List(flow));
    }

    [TestMethod]
    public void UpdateDeleteAndList_MutateCurrentFlowOnly()
    {
        RuleReviewIssueStateStore store = new();
        RuleFlowKey firstFlow = CreateFlow("task-1");
        RuleFlowKey secondFlow = CreateFlow("task-2");

        StoredRuleReviewIssue first = store.Add(firstFlow, CreateIssue("Program.cs"), "first");
        store.Add(secondFlow, CreateIssue("Cache.cs"), "second");

        StoredRuleReviewIssue updated = store.Update(firstFlow, first.RuleReviewIssueId, CreateIssue("Updated.cs"));
        bool deleted = store.Delete(firstFlow, updated.RuleReviewIssueId);

        Assert.AreEqual("Updated.cs", updated.FileOrFunction);
        Assert.IsTrue(deleted);
        Assert.IsEmpty(store.List(firstFlow));
        Assert.HasCount(1, store.List(secondFlow));
    }

    [TestMethod]
    public void CloneRestore_RewindsState()
    {
        RuleReviewIssueStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");
        store.Add(flow, CreateIssue("Program.cs"), "first");
        RuleReviewFlowState? snapshot = store.Clone(flow);

        store.Add(flow, CreateIssue("Stale.cs"), "second");
        store.Restore(flow, snapshot);

        IReadOnlyList<StoredRuleReviewIssue> issues = store.List(flow);
        Assert.HasCount(1, issues);
        Assert.AreEqual("Program.cs", issues[0].FileOrFunction);
    }

    [TestMethod]
    public void SubmitNoIssueConclusion_Throws_WhenIssuesExist()
    {
        RuleReviewIssueStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");
        store.Add(flow, CreateIssue("Program.cs"), "first");

        Assert.ThrowsExactly<InvalidOperationException>(() => store.SubmitNoIssueConclusion(flow, CreateConclusion()));
    }

    private static NormalizedRuleIssue CreateIssue(string fileOrFunction) =>
        RuleIssueNormalizer.NormalizeToContract(new RuleReviewIssue
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
        RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", taskItemId, "performance");
}
