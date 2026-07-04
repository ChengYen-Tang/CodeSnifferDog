using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Attempts;
using CodeSnifferDog.Modules.Tools.Issues;
using CodeSnifferDog.Modules.Tools.RuleReview.State;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

/// <summary>
/// Stores rule-review issues in memory with retry-safe rollback support.
/// </summary>
public sealed class InMemoryIssueStore : IIssueStore
{
    private readonly IssueStateStore _stateStore = new();
    private readonly ScopedAttemptWriteGuard<RuleFlowKey> _writeGuard = new();
    private readonly Lock _syncRoot = new();

    /// <inheritdoc />
    public ValueTask<StoredIssue> AddAsync(
        RuleFlowKey ruleFlowKey,
        Issue issue,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(issue);
        NormalizedRuleIssue normalizedIssue = RuleIssueNormalizer.NormalizeToContract(issue);
        StoredIssue generatedIssue = RuleIssueStoreMapper.CreateReviewIssue(
            normalizedIssue,
            Guid.NewGuid().ToString("N"));

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.FromResult(generatedIssue);

            return ValueTask.FromResult(_stateStore.Add(
                ruleFlowKey,
                normalizedIssue,
                generatedIssue.RuleReviewIssueId));
        }
    }

    /// <inheritdoc />
    public ValueTask<StoredIssue> GetAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReviewIssueId,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReviewIssueId);

        lock (_syncRoot)
            return ValueTask.FromResult(_stateStore.Get(ruleFlowKey, ruleReviewIssueId));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<StoredIssue>> ListAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_stateStore.List(ruleFlowKey));
    }

    /// <inheritdoc />
    public ValueTask<StoredIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReviewIssueId,
        Issue issue,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReviewIssueId);
        ArgumentNullException.ThrowIfNull(issue);
        NormalizedRuleIssue normalizedIssue = RuleIssueNormalizer.NormalizeToContract(issue);

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.FromResult(_stateStore.Get(ruleFlowKey, ruleReviewIssueId));

            return ValueTask.FromResult(_stateStore.Update(ruleFlowKey, ruleReviewIssueId, normalizedIssue));
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReviewIssueId,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReviewIssueId);

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.FromResult(false);

            return ValueTask.FromResult(_stateStore.Delete(ruleFlowKey, ruleReviewIssueId));
        }
    }

    /// <inheritdoc />
    public ValueTask<NoIssueConclusion?> GetNoIssueConclusionAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_stateStore.GetNoIssueConclusion(ruleFlowKey));
    }

    /// <inheritdoc />
    public ValueTask SubmitNoIssueConclusionAsync(
        RuleFlowKey ruleFlowKey,
        NoIssueConclusion conclusion,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(conclusion);
        NoIssueConclusion normalizedConclusion = NormalizeNoIssueConclusion(conclusion);

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            _stateStore.SubmitNoIssueConclusion(ruleFlowKey, normalizedConclusion);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(RuleFlowKey ruleFlowKey, CancellationToken _)
    {
        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            _stateStore.Clear(ruleFlowKey);
            _writeGuard.Clear(ruleFlowKey);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public IAgentAttemptLease BeginAttempt(RuleFlowKey ruleFlowKey, Guid attemptId)
    {
        lock (_syncRoot)
        {
            FlowState? snapshot = _stateStore.Clone(ruleFlowKey);
            return _writeGuard.BeginAttempt(
                ruleFlowKey,
                attemptId,
                () => _stateStore.Restore(ruleFlowKey, snapshot));
        }
    }

    /// <summary>
    /// Normalizes a no-issue conclusion before storage.
    /// </summary>
    /// <param name="conclusion">Conclusion to normalize.</param>
    /// <returns>The normalized conclusion.</returns>
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
}
