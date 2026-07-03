using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Workflows.Common;
using CodeSnifferDog.Workflows.Report;
using StoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Tests.Workflows.Report;

[TestClass]
public sealed class DiffServiceTests
{
    private static readonly RuleReportKey RuleReportKey = new(@"Z:\RepoA", "performance");
    private static readonly RuleFlowKey RuleFlowKey = new(@"Z:\RepoA", "task-1", "performance");

    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task ComputeAndStoreDiffAsync_WhenSnapshotEmpty_AddsAllCurrentIssuesToCreated()
    {
        StoredIssue currentIssue = CreateIssue("issue-1");
        FakeIssueStore store = new()
        {
            PreviousSnapshot = [],
            CurrentIssues = [currentIssue],
        };
        DiffService service = new(store);

        Diff diff = await service.ComputeAndStoreDiffAsync(
            RuleReportKey,
            RuleFlowKey,
            TestContext.CancellationToken);

        Assert.HasCount(1, diff.CreatedIssues);
        Assert.AreSame(currentIssue, diff.CreatedIssues[0]);
        Assert.IsEmpty(diff.UpdatedIssues);
        Assert.IsEmpty(diff.DeletedIssues);
    }

    [TestMethod]
    public async Task ComputeAndStoreDiffAsync_WhenSnapshotIssueMissingFromCurrent_AddsIssueToDeleted()
    {
        StoredIssue previousIssue = CreateIssue("issue-1");
        FakeIssueStore store = new()
        {
            PreviousSnapshot = [previousIssue],
            CurrentIssues = [],
        };
        DiffService service = new(store);

        Diff diff = await service.ComputeAndStoreDiffAsync(
            RuleReportKey,
            RuleFlowKey,
            TestContext.CancellationToken);

        Assert.IsEmpty(diff.CreatedIssues);
        Assert.IsEmpty(diff.UpdatedIssues);
        Assert.HasCount(1, diff.DeletedIssues);
        Assert.AreSame(previousIssue, diff.DeletedIssues[0]);
    }

    [TestMethod]
    public async Task ComputeAndStoreDiffAsync_WhenSameIdHasChangedField_AddsCurrentIssueToUpdated()
    {
        StoredIssue previousIssue = CreateIssue("issue-1", suggestedFixDirection: "Investigate the hot path.");
        StoredIssue currentIssue = CreateIssue("issue-1", suggestedFixDirection: "Use a cached async path.");
        FakeIssueStore store = new()
        {
            PreviousSnapshot = [previousIssue],
            CurrentIssues = [currentIssue],
        };
        DiffService service = new(store);

        Diff diff = await service.ComputeAndStoreDiffAsync(
            RuleReportKey,
            RuleFlowKey,
            TestContext.CancellationToken);

        Assert.IsEmpty(diff.CreatedIssues);
        Assert.HasCount(1, diff.UpdatedIssues);
        Assert.AreSame(currentIssue, diff.UpdatedIssues[0]);
        Assert.IsEmpty(diff.DeletedIssues);
    }

    [TestMethod]
    [DataRow(nameof(StoredIssue.IssueType))]
    [DataRow(nameof(StoredIssue.Severity))]
    [DataRow(nameof(StoredIssue.FileOrFunction))]
    [DataRow(nameof(StoredIssue.RelevantCodePatternOrExpression))]
    [DataRow(nameof(StoredIssue.WhyThisIsAProblem))]
    [DataRow(nameof(StoredIssue.Confidence))]
    [DataRow(nameof(StoredIssue.FollowUpFiles))]
    [DataRow(nameof(StoredIssue.SuggestedFixDirection))]
    [DataRow(nameof(StoredIssue.ReviewStrategy))]
    [DataRow(nameof(StoredIssue.ScopeCoverage))]
    [DataRow(nameof(StoredIssue.CrossScopeAnalysis))]
    public async Task ComputeAndStoreDiffAsync_WhenAnySameIdEquivalenceFieldChanges_AddsCurrentIssueToUpdated(
        string changedField)
    {
        StoredIssue previousIssue = CreateIssue("issue-1");
        StoredIssue currentIssue = CreateIssueWithChangedField(changedField);
        FakeIssueStore store = new()
        {
            PreviousSnapshot = [previousIssue],
            CurrentIssues = [currentIssue],
        };
        DiffService service = new(store);

        Diff diff = await service.ComputeAndStoreDiffAsync(
            RuleReportKey,
            RuleFlowKey,
            TestContext.CancellationToken);

        Assert.IsEmpty(diff.CreatedIssues);
        Assert.HasCount(1, diff.UpdatedIssues);
        Assert.AreSame(currentIssue, diff.UpdatedIssues[0]);
        Assert.IsEmpty(diff.DeletedIssues);
    }

    [TestMethod]
    public async Task ComputeAndStoreDiffAsync_WhenSameIdHasIdenticalFields_DoesNotUpdateIssue()
    {
        StoredIssue previousIssue = CreateIssue("issue-1");
        StoredIssue currentIssue = CreateIssue("issue-1");
        FakeIssueStore store = new()
        {
            PreviousSnapshot = [previousIssue],
            CurrentIssues = [currentIssue],
        };
        DiffService service = new(store);

        Diff diff = await service.ComputeAndStoreDiffAsync(
            RuleReportKey,
            RuleFlowKey,
            TestContext.CancellationToken);

        Assert.IsEmpty(diff.CreatedIssues);
        Assert.IsEmpty(diff.UpdatedIssues);
        Assert.IsEmpty(diff.DeletedIssues);
    }

    [TestMethod]
    public async Task ComputeAndStoreDiffAsync_StoresComputedDiffAndReturnsSameInstance()
    {
        StoredIssue currentIssue = CreateIssue("issue-1");
        FakeIssueStore store = new()
        {
            PreviousSnapshot = [],
            CurrentIssues = [currentIssue],
        };
        DiffService service = new(store);

        Diff diff = await service.ComputeAndStoreDiffAsync(
            RuleReportKey,
            RuleFlowKey,
            TestContext.CancellationToken);

        Assert.AreEqual(RuleFlowKey, store.StoredRuleFlowKey);
        Assert.AreSame(diff, store.StoredDiff);
        Diff storedDiff = store.StoredDiff ?? throw new InvalidOperationException("Expected stored diff.");
        Assert.HasCount(1, storedDiff.CreatedIssues);
        Assert.AreSame(currentIssue, storedDiff.CreatedIssues[0]);
    }

    private static StoredIssue CreateIssue(
        string ruleReportIssueId,
        string issueType = "Performance",
        string severity = "High",
        string fileOrFunction = "Program.cs",
        string relevantCodePatternOrExpression = "Repeated synchronous call",
        string whyThisIsAProblem = "This blocks the hot path.",
        string confidence = "High",
        string followUpFiles = "Program.cs",
        string suggestedFixDirection = "Use a cached async path.",
        string reviewStrategy = "Reviewed the hot path first.",
        string scopeCoverage = "Inspected Program.cs.",
        string crossScopeAnalysis = "No cross-scope inspection was required.")
        =>
        new()
        {
            RuleReportIssueId = ruleReportIssueId,
            IssueType = issueType,
            Severity = severity,
            FileOrFunction = fileOrFunction,
            RelevantCodePatternOrExpression = relevantCodePatternOrExpression,
            WhyThisIsAProblem = whyThisIsAProblem,
            Confidence = confidence,
            FollowUpFiles = followUpFiles,
            SuggestedFixDirection = suggestedFixDirection,
            ReviewStrategy = reviewStrategy,
            ScopeCoverage = scopeCoverage,
            CrossScopeAnalysis = crossScopeAnalysis,
        };

    private static StoredIssue CreateIssueWithChangedField(string changedField)
    {
        return changedField switch
        {
            nameof(StoredIssue.IssueType) => CreateIssue("issue-1", issueType: "Reliability"),
            nameof(StoredIssue.Severity) => CreateIssue("issue-1", severity: "Medium"),
            nameof(StoredIssue.FileOrFunction) => CreateIssue("issue-1", fileOrFunction: "Cache.cs"),
            nameof(StoredIssue.RelevantCodePatternOrExpression) => CreateIssue("issue-1", relevantCodePatternOrExpression: "Repeated cache miss"),
            nameof(StoredIssue.WhyThisIsAProblem) => CreateIssue("issue-1", whyThisIsAProblem: "This repeatedly misses the cache."),
            nameof(StoredIssue.Confidence) => CreateIssue("issue-1", confidence: "Medium"),
            nameof(StoredIssue.FollowUpFiles) => CreateIssue("issue-1", followUpFiles: "Program.cs;Cache.cs"),
            nameof(StoredIssue.SuggestedFixDirection) => CreateIssue("issue-1", suggestedFixDirection: "Investigate the hot path."),
            nameof(StoredIssue.ReviewStrategy) => CreateIssue(
                "issue-1",
                reviewStrategy: "Reviewed the cache path first."),
            nameof(StoredIssue.ScopeCoverage) => CreateIssue(
                "issue-1",
                scopeCoverage: "Inspected Program.cs and Cache.cs."),
            nameof(StoredIssue.CrossScopeAnalysis) => CreateIssue(
                "issue-1",
                crossScopeAnalysis: "Compared Program.cs with Cache.cs."),
            _ => throw new ArgumentOutOfRangeException(nameof(changedField), changedField, "Unsupported changed field."),
        };
    }

    private sealed class FakeIssueStore : IIssueStore
    {
        public IReadOnlyList<StoredIssue> PreviousSnapshot { get; init; } = [];

        public IReadOnlyList<StoredIssue> CurrentIssues { get; init; } = [];

        public RuleFlowKey? StoredRuleFlowKey { get; private set; }

        public Diff? StoredDiff { get; private set; }

        public IAgentAttemptLease BeginAttempt(RuleFlowKey scope, Guid attemptId) => new NoOpAttemptLease();

        public ValueTask InitializeWorkingReportAsync(
            RuleReportKey ruleReportKey,
            string ruleKey,
            RuleFlowKey ruleFlowKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<StoredIssue> AddAsync(
            RuleFlowKey ruleFlowKey,
            Issue issue,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<StoredIssue> GetAsync(
            RuleFlowKey ruleFlowKey,
            string ruleReportIssueId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<StoredIssue>> ListAsync(
            RuleFlowKey ruleFlowKey,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(CurrentIssues);

        public ValueTask<StoredIssue> UpdateAsync(
            RuleFlowKey ruleFlowKey,
            string ruleReportIssueId,
            Issue issue,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(
            RuleFlowKey ruleFlowKey,
            string ruleReportIssueId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<StoredIssue>> GetLatestSnapshotAsync(
            RuleReportKey ruleReportKey,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(PreviousSnapshot);

        public ValueTask<Diff> GetLatestDiffAsync(
            RuleFlowKey ruleFlowKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask SetLatestDiffAsync(
            RuleFlowKey ruleFlowKey,
            Diff diff,
            CancellationToken cancellationToken)
        {
            StoredRuleFlowKey = ruleFlowKey;
            StoredDiff = diff;
            return ValueTask.CompletedTask;
        }

        public ValueTask PromoteWorkingReportAsync(
            RuleReportKey ruleReportKey,
            RuleFlowKey ruleFlowKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask ClearWorkingReportAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask ClearAsync(
            RuleReportKey ruleReportKey,
            RuleFlowKey ruleFlowKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpAttemptLease : IAgentAttemptLease
    {
        public void Restore()
        {
        }
    }
}
