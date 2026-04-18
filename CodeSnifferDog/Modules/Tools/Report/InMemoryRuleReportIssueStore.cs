using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.Report;

public sealed class InMemoryRuleReportIssueStore : IRuleReportIssueStore
{
    private readonly Dictionary<RuleReportKey, List<StoredRuleReportIssue>> _latestSnapshots = [];
    private readonly Dictionary<RuleFlowKey, RuleReportFlowState> _flowStates = [];
    private readonly Lock _syncRoot = new();

    public ValueTask InitializeWorkingReportAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            List<StoredRuleReportIssue> latestSnapshot = GetOrCreateLatestSnapshot(ruleReportKey);
            RuleReportFlowState flowState = GetOrCreateFlowState(ruleFlowKey);
            flowState.WorkingIssues.Clear();

            foreach (StoredRuleReportIssue issue in latestSnapshot)
                flowState.WorkingIssues.Add(Clone(issue));

            flowState.LatestDiff = CreateEmptyDiff();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<StoredRuleReportIssue> AddAsync(
        RuleFlowKey ruleFlowKey,
        RuleReviewIssue issue,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issue);
        StoredRuleReportIssue storedIssue = CreateStoredIssue(NormalizeIssue(issue), Guid.NewGuid().ToString("N"));

        lock (_syncRoot)
            GetOrCreateFlowState(ruleFlowKey).WorkingIssues.Add(storedIssue);

        return ValueTask.FromResult(storedIssue);
    }

    public ValueTask<StoredRuleReportIssue> GetAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReportIssueId);

        lock (_syncRoot)
        {
            StoredRuleReportIssue? issue = GetOrCreateFlowState(ruleFlowKey).WorkingIssues
                .FirstOrDefault(item => item.RuleReportIssueId == ruleReportIssueId.Trim());

            if (issue is null)
                throw new KeyNotFoundException($"Rule report issue was not found: {ruleReportIssueId}");

            return ValueTask.FromResult(issue);
        }
    }

    public ValueTask<IReadOnlyList<StoredRuleReportIssue>> ListAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<IReadOnlyList<StoredRuleReportIssue>>([.. GetOrCreateFlowState(ruleFlowKey).WorkingIssues]);
    }

    public ValueTask<StoredRuleReportIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        RuleReviewIssue issue,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReportIssueId);
        ArgumentNullException.ThrowIfNull(issue);
        RuleReviewIssue normalizedIssue = NormalizeIssue(issue);

        lock (_syncRoot)
        {
            RuleReportFlowState flowState = GetOrCreateFlowState(ruleFlowKey);
            int index = flowState.WorkingIssues.FindIndex(item => item.RuleReportIssueId == ruleReportIssueId.Trim());

            if (index < 0)
                throw new KeyNotFoundException($"Rule report issue was not found: {ruleReportIssueId}");

            StoredRuleReportIssue storedIssue = CreateStoredIssue(normalizedIssue, flowState.WorkingIssues[index].RuleReportIssueId);
            flowState.WorkingIssues[index] = storedIssue;
            return ValueTask.FromResult(storedIssue);
        }
    }

    public ValueTask<bool> DeleteAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReportIssueId);

        lock (_syncRoot)
        {
            RuleReportFlowState flowState = GetOrCreateFlowState(ruleFlowKey);
            StoredRuleReportIssue? issue = flowState.WorkingIssues.FirstOrDefault(item => item.RuleReportIssueId == ruleReportIssueId.Trim());

            if (issue is null)
                return ValueTask.FromResult(false);

            flowState.WorkingIssues.Remove(issue);
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<IReadOnlyList<StoredRuleReportIssue>> GetLatestSnapshotAsync(
        RuleReportKey ruleReportKey,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<IReadOnlyList<StoredRuleReportIssue>>([.. GetOrCreateLatestSnapshot(ruleReportKey)]);
    }

    public ValueTask<RuleReportDiff> GetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(GetOrCreateFlowState(ruleFlowKey).LatestDiff);
    }

    public ValueTask SetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        RuleReportDiff diff,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diff);

        lock (_syncRoot)
            GetOrCreateFlowState(ruleFlowKey).LatestDiff = diff;

        return ValueTask.CompletedTask;
    }

    public ValueTask PromoteWorkingReportAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            List<StoredRuleReportIssue> latestSnapshot = GetOrCreateLatestSnapshot(ruleReportKey);
            RuleReportFlowState flowState = GetOrCreateFlowState(ruleFlowKey);
            latestSnapshot.Clear();

            foreach (StoredRuleReportIssue issue in flowState.WorkingIssues)
                latestSnapshot.Add(Clone(issue));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearWorkingReportAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (!_flowStates.TryGetValue(ruleFlowKey, out RuleReportFlowState? flowState))
                return ValueTask.CompletedTask;

            flowState.WorkingIssues.Clear();
            flowState.LatestDiff = CreateEmptyDiff();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (_latestSnapshots.ContainsKey(ruleReportKey))
                _latestSnapshots.Remove(ruleReportKey);

            if (_flowStates.ContainsKey(ruleFlowKey))
                _flowStates.Remove(ruleFlowKey);
        }

        return ValueTask.CompletedTask;
    }

    private List<StoredRuleReportIssue> GetOrCreateLatestSnapshot(RuleReportKey ruleReportKey)
    {
        if (_latestSnapshots.TryGetValue(ruleReportKey, out List<StoredRuleReportIssue>? state))
            return state;

        state = [];
        _latestSnapshots.Add(ruleReportKey, state);
        return state;
    }

    private RuleReportFlowState GetOrCreateFlowState(RuleFlowKey ruleFlowKey)
    {
        if (_flowStates.TryGetValue(ruleFlowKey, out RuleReportFlowState? state))
            return state;

        state = new RuleReportFlowState();
        _flowStates.Add(ruleFlowKey, state);
        return state;
    }

    private static RuleReportDiff CreateEmptyDiff()
        =>
        new()
        {
            CreatedIssues = [],
            UpdatedIssues = [],
            DeletedIssues = [],
        };

    private static RuleReviewIssue NormalizeIssue(RuleReviewIssue issue)
    {
        ValidateIssue(issue);
        return new RuleReviewIssue
        {
            IssueType = issue.IssueType.Trim(),
            FileOrFunction = issue.FileOrFunction.Trim(),
            RelevantCodePatternOrExpression = issue.RelevantCodePatternOrExpression.Trim(),
            WhyThisIsAProblem = issue.WhyThisIsAProblem.Trim(),
            Confidence = issue.Confidence.Trim(),
            FollowUpFiles = issue.FollowUpFiles.Trim(),
            SuggestedFixDirection = issue.SuggestedFixDirection.Trim(),
            ReviewStrategy = issue.ReviewStrategy.Trim(),
            ScopeCoverage = issue.ScopeCoverage.Trim(),
            CrossScopeAnalysis = issue.CrossScopeAnalysis.Trim(),
        };
    }

    private static StoredRuleReportIssue CreateStoredIssue(RuleReviewIssue issue, string id)
        =>
        new()
        {
            RuleReportIssueId = id,
            IssueType = issue.IssueType,
            FileOrFunction = issue.FileOrFunction,
            RelevantCodePatternOrExpression = issue.RelevantCodePatternOrExpression,
            WhyThisIsAProblem = issue.WhyThisIsAProblem,
            Confidence = issue.Confidence,
            FollowUpFiles = issue.FollowUpFiles,
            SuggestedFixDirection = issue.SuggestedFixDirection,
            ReviewStrategy = issue.ReviewStrategy,
            ScopeCoverage = issue.ScopeCoverage,
            CrossScopeAnalysis = issue.CrossScopeAnalysis,
        };

    private static StoredRuleReportIssue Clone(StoredRuleReportIssue issue)
        =>
        new()
        {
            RuleReportIssueId = issue.RuleReportIssueId,
            IssueType = issue.IssueType,
            FileOrFunction = issue.FileOrFunction,
            RelevantCodePatternOrExpression = issue.RelevantCodePatternOrExpression,
            WhyThisIsAProblem = issue.WhyThisIsAProblem,
            Confidence = issue.Confidence,
            FollowUpFiles = issue.FollowUpFiles,
            SuggestedFixDirection = issue.SuggestedFixDirection,
            ReviewStrategy = issue.ReviewStrategy,
            ScopeCoverage = issue.ScopeCoverage,
            CrossScopeAnalysis = issue.CrossScopeAnalysis,
        };

    private static void ValidateIssue(RuleReviewIssue issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.IssueType);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.FileOrFunction);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.RelevantCodePatternOrExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.WhyThisIsAProblem);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.Confidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.FollowUpFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.SuggestedFixDirection);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.ReviewStrategy);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.ScopeCoverage);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.CrossScopeAnalysis);
    }

    private sealed class RuleReportFlowState
    {
        public List<StoredRuleReportIssue> WorkingIssues { get; } = [];

        public RuleReportDiff LatestDiff { get; set; } = CreateEmptyDiff();
    }
}
