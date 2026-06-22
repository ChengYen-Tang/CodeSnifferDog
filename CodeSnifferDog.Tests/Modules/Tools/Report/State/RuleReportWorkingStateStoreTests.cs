using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;
using CodeSnifferDog.Modules.Tools.Report.State;

namespace CodeSnifferDog.Tests.Modules.Tools.Report.State;

[TestClass]
public sealed class RuleReportWorkingStateStoreTests
{
    [TestMethod]
    public void Initialize_CopiesSnapshotIssuesAndResetsDiff()
    {
        RuleReportWorkingStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");
        StoredRuleReportIssue snapshotIssue = RuleIssueStoreMapper.CreateReportIssue(CreateIssue("Program.cs"), "snapshot");
        store.SetLatestDiff(flow, CreateDiff("stale"));

        store.Initialize(flow, [snapshotIssue]);

        IReadOnlyList<StoredRuleReportIssue> issues = store.List(flow);
        RuleReportDiff diff = store.GetLatestDiff(flow);

        Assert.HasCount(1, issues);
        Assert.AreEqual("Program.cs", issues[0].FileOrFunction);
        Assert.IsEmpty(diff.CreatedIssues);
        Assert.IsEmpty(diff.UpdatedIssues);
        Assert.IsEmpty(diff.DeletedIssues);
        Assert.AreNotSame(snapshotIssue, issues[0]);
    }

    [TestMethod]
    public void AddUpdateDelete_DeduplicatesAndMutatesWorkingIssues()
    {
        RuleReportWorkingStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");

        StoredRuleReportIssue first = store.Add(flow, CreateIssue(" Program.cs "), "first");
        StoredRuleReportIssue duplicate = store.Add(flow, CreateIssue("Program.cs"), "duplicate");
        StoredRuleReportIssue updated = store.Update(flow, first.RuleReportIssueId, CreateIssue("Updated.cs"));
        bool deleted = store.Delete(flow, updated.RuleReportIssueId);

        Assert.AreSame(first, duplicate);
        Assert.AreEqual("Updated.cs", updated.FileOrFunction);
        Assert.IsTrue(deleted);
        Assert.IsEmpty(store.List(flow));
    }

    [TestMethod]
    public void CloneRestore_RewindsIssuesAndDiff()
    {
        RuleReportWorkingStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");
        store.Add(flow, CreateIssue("Program.cs"), "first");
        store.SetLatestDiff(flow, CreateDiff("before"));
        RuleReportFlowState? snapshot = store.Clone(flow);

        store.Add(flow, CreateIssue("Stale.cs"), "second");
        store.SetLatestDiff(flow, CreateDiff("after"));
        store.Restore(flow, snapshot);

        IReadOnlyList<StoredRuleReportIssue> issues = store.List(flow);
        RuleReportDiff diff = store.GetLatestDiff(flow);
        Assert.HasCount(1, issues);
        Assert.AreEqual("Program.cs", issues[0].FileOrFunction);
        Assert.AreEqual("before", diff.UpdatedIssues[0].RuleReportIssueId);
    }

    [TestMethod]
    public void Clear_RemovesWorkingIssuesAndResetsDiff()
    {
        RuleReportWorkingStateStore store = new();
        RuleFlowKey flow = CreateFlow("task-1");
        store.Add(flow, CreateIssue("Program.cs"), "first");
        store.SetLatestDiff(flow, CreateDiff("before"));

        store.Clear(flow);

        Assert.IsEmpty(store.List(flow));
        Assert.IsEmpty(store.GetLatestDiff(flow).UpdatedIssues);
    }

    private static RuleReportDiff CreateDiff(string id) =>
        new()
        {
            CreatedIssues = [],
            UpdatedIssues =
            [
                new StoredRuleReportIssue
                {
                    RuleReportIssueId = id,
                    IssueType = "Performance",
                    Severity = "High",
                    FileOrFunction = "Program.cs",
                    RelevantCodePatternOrExpression = "call",
                    WhyThisIsAProblem = "problem",
                    Confidence = "High",
                    FollowUpFiles = "Program.cs",
                    SuggestedFixDirection = "fix",
                    ScopeCoverage = "scope",
                    CrossScopeAnalysis = "cross",
                    ReviewStrategy = "strategy",
                },
            ],
            DeletedIssues = [],
        };

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

    private static RuleFlowKey CreateFlow(string taskItemId) =>
        RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", taskItemId, "performance");
}
