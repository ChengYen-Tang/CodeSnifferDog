using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

public sealed class InMemoryRuleReviewIssueStore : IRuleReviewIssueStore
{
    private readonly Dictionary<RuleFlowKey, RuleReviewFlowState> _states = [];
    private readonly Dictionary<RuleFlowKey, Guid> _activeAttemptIds = [];
    private readonly Lock _syncRoot = new();

    public ValueTask<StoredRuleReviewIssue> AddAsync(
        RuleFlowKey ruleFlowKey,
        RuleReviewIssue issue,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(issue);
        RuleReviewIssue normalizedIssue = NormalizeIssue(issue);
        StoredRuleReviewIssue storedIssue = RuleIssueStoreMapper.CreateReviewIssue(
            normalizedIssue,
            Guid.NewGuid().ToString("N"));

        lock (_syncRoot)
        {
            if (!CanWrite(ruleFlowKey))
                return ValueTask.FromResult(storedIssue);

            RuleReviewFlowState state = GetOrCreateState(ruleFlowKey);
            state.NoIssueConclusion = null;
            StoredRuleReviewIssue? existingIssue = state.Issues
                .FirstOrDefault(candidate => RuleIssueStoreMapper.IsEquivalentToNormalizedIssue(candidate, normalizedIssue));
            if (existingIssue is not null)
                return ValueTask.FromResult(existingIssue);

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
            if (!CanWrite(ruleFlowKey))
            {
                RuleReviewFlowState existingState = GetOrCreateState(ruleFlowKey);
                StoredRuleReviewIssue existingIssue = existingState.Issues
                    .FirstOrDefault(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim())
                    ?? throw new KeyNotFoundException($"Rule review issue was not found: {ruleReviewIssueId}");
                return ValueTask.FromResult(existingIssue);
            }

            RuleReviewFlowState state = GetOrCreateState(ruleFlowKey);
            int index = state.Issues.FindIndex(item => item.RuleReviewIssueId == ruleReviewIssueId.Trim());

            if (index < 0)
                throw new KeyNotFoundException($"Rule review issue was not found: {ruleReviewIssueId}");

            StoredRuleReviewIssue storedIssue = RuleIssueStoreMapper.CreateReviewIssue(
                normalizedIssue,
                state.Issues[index].RuleReviewIssueId);
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
            if (!CanWrite(ruleFlowKey))
                return ValueTask.FromResult(false);

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
            if (!CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

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
            if (!CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            _states.Remove(ruleFlowKey);
            _activeAttemptIds.Remove(ruleFlowKey);
        }

        return ValueTask.CompletedTask;
    }

    public IAgentAttemptLease BeginAttempt(RuleFlowKey ruleFlowKey, Guid attemptId)
    {
        lock (_syncRoot)
        {
            _states.TryGetValue(ruleFlowKey, out RuleReviewFlowState? previousState);
            Guid staleWriteBlockerAttemptId = Guid.NewGuid();
            RuleReviewFlowState? snapshot = previousState?.Clone();
            _activeAttemptIds[ruleFlowKey] = attemptId;

            return new AgentAttemptLease(() =>
            {
                lock (_syncRoot)
                {
                    _activeAttemptIds[ruleFlowKey] = staleWriteBlockerAttemptId;

                    if (snapshot is null)
                        _states.Remove(ruleFlowKey);
                    else
                        _states[ruleFlowKey] = snapshot.Clone();
                }
            });
        }
    }

    private RuleReviewFlowState GetOrCreateState(RuleFlowKey ruleFlowKey)
    {
        if (_states.TryGetValue(ruleFlowKey, out RuleReviewFlowState? state))
            return state;

        state = new RuleReviewFlowState();
        _states.Add(ruleFlowKey, state);
        return state;
    }

    private static RuleReviewIssue NormalizeIssue(RuleReviewIssue issue) =>
        RuleIssueNormalizer.Normalize(issue);

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

    private bool CanWrite(RuleFlowKey ruleFlowKey)
    {
        Guid? currentAttemptId = AgentRunAttemptContext.CurrentAttemptId;
        return currentAttemptId is null ||
            !_activeAttemptIds.TryGetValue(ruleFlowKey, out Guid activeAttemptId) ||
            currentAttemptId == activeAttemptId;
    }

    internal sealed class RuleReviewFlowState
    {
        public List<StoredRuleReviewIssue> Issues { get; } = [];

        public NoIssueConclusion? NoIssueConclusion { get; set; }

        public RuleReviewFlowState Clone()
        {
            RuleReviewFlowState clone = new()
            {
                NoIssueConclusion = NoIssueConclusion is null
                    ? null
                    : new NoIssueConclusion
                    {
                        ReviewStrategy = NoIssueConclusion.ReviewStrategy,
                        ScopeCoverage = NoIssueConclusion.ScopeCoverage,
                        CrossScopeAnalysis = NoIssueConclusion.CrossScopeAnalysis,
                        WhyNoIssueWasFound = NoIssueConclusion.WhyNoIssueWasFound,
                    },
            };

            clone.Issues.AddRange(Issues.Select(RuleIssueStoreMapper.Clone));
            return clone;
        }
    }
}
