using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

public sealed class InMemoryRuleReviewIssueStore : IRuleReviewIssueStore
{
    private readonly Dictionary<RuleFlowKey, RuleReviewFlowState> _states = [];
    private readonly Lock _syncRoot = new();

    public ValueTask<StoredRuleReviewIssue> AddAsync(
        RuleFlowKey ruleFlowKey,
        RuleReviewIssue issue,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(issue);
        RuleReviewIssue normalizedIssue = NormalizeIssue(issue);
        StoredRuleReviewIssue storedIssue = new()
        {
            RuleReviewIssueId = Guid.NewGuid().ToString("N"),
            IssueType = normalizedIssue.IssueType,
            Severity = normalizedIssue.Severity,
            FileOrFunction = normalizedIssue.FileOrFunction,
            RelevantCodePatternOrExpression = normalizedIssue.RelevantCodePatternOrExpression,
            WhyThisIsAProblem = normalizedIssue.WhyThisIsAProblem,
            Confidence = normalizedIssue.Confidence,
            FollowUpFiles = normalizedIssue.FollowUpFiles,
            SuggestedFixDirection = normalizedIssue.SuggestedFixDirection,
            ReviewStrategy = normalizedIssue.ReviewStrategy,
            ScopeCoverage = normalizedIssue.ScopeCoverage,
            CrossScopeAnalysis = normalizedIssue.CrossScopeAnalysis,
        };

        lock (_syncRoot)
        {
            RuleReviewFlowState state = GetOrCreateState(ruleFlowKey);
            state.NoIssueConclusion = null;
            state.Issues.Add(storedIssue);
        }

        return ValueTask.FromResult(storedIssue);
    }

    public ValueTask<StoredRuleReviewIssue> GetAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReviewIssueId,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReviewIssueId);

        lock (_syncRoot)
        {
            return ValueTask.FromResult(
                GetOrCreateState(ruleFlowKey).Issues
                    .FirstOrDefault(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim())
                ?? throw new KeyNotFoundException($"Rule review issue was not found: {ruleReviewIssueId}"));
        }
    }

    public ValueTask<IReadOnlyList<StoredRuleReviewIssue>> ListAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<IReadOnlyList<StoredRuleReviewIssue>>([.. GetOrCreateState(ruleFlowKey).Issues]);
    }

    public ValueTask<StoredRuleReviewIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReviewIssueId,
        RuleReviewIssue issue,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReviewIssueId);
        ArgumentNullException.ThrowIfNull(issue);
        RuleReviewIssue normalizedIssue = NormalizeIssue(issue);

        lock (_syncRoot)
        {
            RuleReviewFlowState state = GetOrCreateState(ruleFlowKey);
            int index = state.Issues.FindIndex(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim());

            if (index < 0)
                throw new KeyNotFoundException($"Rule review issue was not found: {ruleReviewIssueId}");

            StoredRuleReviewIssue storedIssue = new()
            {
                RuleReviewIssueId = state.Issues[index].RuleReviewIssueId,
                IssueType = normalizedIssue.IssueType,
                Severity = normalizedIssue.Severity,
                FileOrFunction = normalizedIssue.FileOrFunction,
                RelevantCodePatternOrExpression = normalizedIssue.RelevantCodePatternOrExpression,
                WhyThisIsAProblem = normalizedIssue.WhyThisIsAProblem,
                Confidence = normalizedIssue.Confidence,
                FollowUpFiles = normalizedIssue.FollowUpFiles,
                SuggestedFixDirection = normalizedIssue.SuggestedFixDirection,
                ReviewStrategy = normalizedIssue.ReviewStrategy,
                ScopeCoverage = normalizedIssue.ScopeCoverage,
                CrossScopeAnalysis = normalizedIssue.CrossScopeAnalysis,
            };
            state.Issues[index] = storedIssue;
            return ValueTask.FromResult(storedIssue);
        }
    }

    public ValueTask<bool> DeleteAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReviewIssueId,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReviewIssueId);

        lock (_syncRoot)
        {
            RuleReviewFlowState state = GetOrCreateState(ruleFlowKey);
            StoredRuleReviewIssue? issue = state.Issues.FirstOrDefault(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim());

            if (issue is null)
                return ValueTask.FromResult(false);

            state.Issues.Remove(issue);
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<NoIssueConclusion?> GetNoIssueConclusionAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(GetOrCreateState(ruleFlowKey).NoIssueConclusion);
    }

    public ValueTask SubmitNoIssueConclusionAsync(
        RuleFlowKey ruleFlowKey,
        NoIssueConclusion conclusion,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(conclusion);
        NoIssueConclusion normalizedConclusion = NormalizeNoIssueConclusion(conclusion);

        lock (_syncRoot)
        {
            RuleReviewFlowState state = GetOrCreateState(ruleFlowKey);

            if (state.Issues.Count > 0)
                throw new InvalidOperationException("Cannot submit a no-issue conclusion while issues exist.");

            state.NoIssueConclusion = normalizedConclusion;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(RuleFlowKey ruleFlowKey, CancellationToken _)
    {
        lock (_syncRoot)
        {
            _states.Remove(ruleFlowKey);
        }

        return ValueTask.CompletedTask;
    }

    private RuleReviewFlowState GetOrCreateState(RuleFlowKey ruleFlowKey)
    {
        if (_states.TryGetValue(ruleFlowKey, out RuleReviewFlowState? state))
            return state;

        state = new RuleReviewFlowState();
        _states.Add(ruleFlowKey, state);
        return state;
    }

    private static RuleReviewIssue NormalizeIssue(RuleReviewIssue issue)
    {
        ValidateIssue(issue);
        return new RuleReviewIssue
        {
            IssueType = issue.IssueType.Trim(),
            Severity = RuleReviewSeverity.Normalize(issue.Severity),
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

    private static void ValidateIssue(RuleReviewIssue issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.IssueType);
        RuleReviewSeverity.Normalize(issue.Severity);
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

    private static NoIssueConclusion NormalizeNoIssueConclusion(NoIssueConclusion conclusion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion.ReviewStrategy);
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion.ScopeCoverage);
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion.CrossScopeAnalysis);
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion.WhyNoIssueWasFound);

        return new NoIssueConclusion
        {
            ReviewStrategy = conclusion.ReviewStrategy.Trim(),
            ScopeCoverage = conclusion.ScopeCoverage.Trim(),
            CrossScopeAnalysis = conclusion.CrossScopeAnalysis.Trim(),
            WhyNoIssueWasFound = conclusion.WhyNoIssueWasFound.Trim(),
        };
    }

    private sealed class RuleReviewFlowState
    {
        public List<StoredRuleReviewIssue> Issues { get; } = [];

        public NoIssueConclusion? NoIssueConclusion { get; set; }
    }
}
