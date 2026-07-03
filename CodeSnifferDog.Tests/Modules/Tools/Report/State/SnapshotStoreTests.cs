using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;
using CodeSnifferDog.Modules.Tools.Report.State;
using StoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Tests.Modules.Tools.Report.State;

[TestClass]
public sealed class SnapshotStoreTests
{
    [TestMethod]
    public void InitializeAndGetSnapshot_CreatesEmptySnapshot()
    {
        SnapshotStore store = new();
        RuleReportKey key = CreateReportKey("performance");

        IReadOnlyList<StoredIssue> issues = store.InitializeAndGetSnapshot(key, " performance ");

        Assert.IsEmpty(issues);
        Assert.IsEmpty(store.GetLatestSnapshot(key));
    }

    [TestMethod]
    public void Promote_CopiesWorkingIssuesToSnapshot()
    {
        SnapshotStore store = new();
        RuleReportKey key = CreateReportKey("performance");
        StoredIssue workingIssue = RuleIssueStoreMapper.CreateReportIssue(CreateIssue("Program.cs"), "working");
        store.InitializeAndGetSnapshot(key, "performance");

        store.Promote(key, [workingIssue]);
        StoredIssue changedWorkingIssue = RuleIssueStoreMapper.CreateReportIssue(CreateIssue("Changed.cs"), "working");

        IReadOnlyList<StoredIssue> snapshot = store.GetLatestSnapshot(key);
        Assert.HasCount(1, snapshot);
        Assert.AreEqual("Program.cs", snapshot[0].FileOrFunction);
        Assert.AreEqual("Changed.cs", changedWorkingIssue.FileOrFunction);
    }

    [TestMethod]
    public void Promote_Throws_WhenSnapshotWasNotInitialized()
    {
        SnapshotStore store = new();

        Assert.ThrowsExactly<KeyNotFoundException>(() => store.Promote(CreateReportKey("performance"), []));
    }

    [TestMethod]
    public void InitializeAndGetSnapshot_Throws_WhenRuleKeyMismatches()
    {
        SnapshotStore store = new();
        RuleReportKey key = CreateReportKey("performance");
        store.InitializeAndGetSnapshot(key, "performance");

        Assert.ThrowsExactly<InvalidOperationException>(() => store.InitializeAndGetSnapshot(key, "memory"));
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

    private static RuleReportKey CreateReportKey(string ruleKey) =>
        RuleScopeKeyFactory.CreateRuleReportKey(TestRepositoryPaths.RootPath, ruleKey);
}
