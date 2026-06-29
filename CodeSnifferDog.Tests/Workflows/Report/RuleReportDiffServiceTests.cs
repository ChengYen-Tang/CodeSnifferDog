using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Workflows.Common;
using CodeSnifferDog.Workflows.Report;

namespace CodeSnifferDog.Tests.Workflows.Report;

[TestClass]
public sealed class RuleReportDiffServiceTests
{
    private static readonly RuleReportKey RuleReportKey = new(@"Z:\RepoA", "performance");
    private static readonly RuleFlowKey RuleFlowKey = new(@"Z:\RepoA", "task-1", "performance");

    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task ComputeAndStoreDiffAsync_WhenSnapshotEmpty_AddsAllCurrentIssuesToCreated()
    {
        StoredRuleReportIssue currentIssue = CreateIssue("issue-1");
        FakeRuleReportIssueStore store = new()
        {
            PreviousSnapshot = [],
            CurrentIssues = [currentIssue],
        };
        RuleReportDiffService service = new(store);

        RuleReportDiff diff = await service.ComputeAndStoreDiffAsync(
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
        StoredRuleReportIssue previousIssue = CreateIssue("issue-1");
        FakeRuleReportIssueStore store = new()
        {
            PreviousSnapshot = [previousIssue],
            CurrentIssues = [],
        };
        RuleReportDiffService service = new(store);

        RuleReportDiff diff = await service.ComputeAndStoreDiffAsync(
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
        StoredRuleReportIssue previousIssue = CreateIssue("issue-1", suggestedFixDirection: "Investigate the hot path.");
        StoredRuleReportIssue currentIssue = CreateIssue("issue-1", suggestedFixDirection: "Use a cached async path.");
        FakeRuleReportIssueStore store = new()
        {
            PreviousSnapshot = [previousIssue],
            CurrentIssues = [currentIssue],
        };
        RuleReportDiffService service = new(store);

        RuleReportDiff diff = await service.ComputeAndStoreDiffAsync(
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
        StoredRuleReportIssue previousIssue = CreateIssue("issue-1");
        StoredRuleReportIssue currentIssue = CreateIssue("issue-1");
        FakeRuleReportIssueStore store = new()
        {
            PreviousSnapshot = [previousIssue],
            CurrentIssues = [currentIssue],
        };
        RuleReportDiffService service = new(store);

        RuleReportDiff diff = await service.ComputeAndStoreDiffAsync(
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
        StoredRuleReportIssue currentIssue = CreateIssue("issue-1");
        FakeRuleReportIssueStore store = new()
        {
            PreviousSnapshot = [],
            CurrentIssues = [currentIssue],
        };
        RuleReportDiffService service = new(store);

        RuleReportDiff diff = await service.ComputeAndStoreDiffAsync(
            RuleReportKey,
            RuleFlowKey,
            TestContext.CancellationToken);

        Assert.AreEqual(RuleFlowKey, store.StoredRuleFlowKey);
        Assert.AreSame(diff, store.StoredDiff);
        RuleReportDiff storedDiff = store.StoredDiff ?? throw new InvalidOperationException("Expected stored diff.");
        Assert.HasCount(1, storedDiff.CreatedIssues);
        Assert.AreSame(currentIssue, storedDiff.CreatedIssues[0]);
    }

    private static StoredRuleReportIssue CreateIssue(
        string ruleReportIssueId,
        string suggestedFixDirection = "Use a cached async path.")
        =>
        new()
        {
            RuleReportIssueId = ruleReportIssueId,
            IssueType = "Performance",
            Severity = "High",
            FileOrFunction = "Program.cs",
            RelevantCodePatternOrExpression = "Repeated synchronous call",
            WhyThisIsAProblem = "This blocks the hot path.",
            Confidence = "High",
            FollowUpFiles = "Program.cs",
            SuggestedFixDirection = suggestedFixDirection,
            ReviewStrategy = "Reviewed the hot path first.",
            ScopeCoverage = "Inspected Program.cs.",
            CrossScopeAnalysis = "No cross-scope inspection was required.",
        };

    private sealed class FakeRuleReportIssueStore : IRuleReportIssueStore
    {
        public IReadOnlyList<StoredRuleReportIssue> PreviousSnapshot { get; init; } = [];

        public IReadOnlyList<StoredRuleReportIssue> CurrentIssues { get; init; } = [];

        public RuleFlowKey? StoredRuleFlowKey { get; private set; }

        public RuleReportDiff? StoredDiff { get; private set; }

        public IAgentAttemptLease BeginAttempt(RuleFlowKey scope, Guid attemptId) => new NoOpAttemptLease();

        public ValueTask InitializeWorkingReportAsync(
            RuleReportKey ruleReportKey,
            string ruleKey,
            RuleFlowKey ruleFlowKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<StoredRuleReportIssue> AddAsync(
            RuleFlowKey ruleFlowKey,
            RuleReviewIssue issue,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<StoredRuleReportIssue> GetAsync(
            RuleFlowKey ruleFlowKey,
            string ruleReportIssueId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<StoredRuleReportIssue>> ListAsync(
            RuleFlowKey ruleFlowKey,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(CurrentIssues);

        public ValueTask<StoredRuleReportIssue> UpdateAsync(
            RuleFlowKey ruleFlowKey,
            string ruleReportIssueId,
            RuleReviewIssue issue,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(
            RuleFlowKey ruleFlowKey,
            string ruleReportIssueId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<StoredRuleReportIssue>> GetLatestSnapshotAsync(
            RuleReportKey ruleReportKey,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(PreviousSnapshot);

        public ValueTask<RuleReportDiff> GetLatestDiffAsync(
            RuleFlowKey ruleFlowKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask SetLatestDiffAsync(
            RuleFlowKey ruleFlowKey,
            RuleReportDiff diff,
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
