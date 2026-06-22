using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Attempts;
using CodeSnifferDog.Modules.Tools.Issues;
using CodeSnifferDog.Modules.Tools.Report.State;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Report;

public sealed class InMemoryRuleReportIssueStore : IRuleReportIssueStore
{
    private readonly RuleReportSnapshotStore _snapshotStore = new();
    private readonly RuleReportWorkingStateStore _workingStateStore = new();
    private readonly ScopedAttemptWriteGuard<RuleFlowKey> _writeGuard = new();
    private readonly Lock _syncRoot = new();

    public ValueTask InitializeWorkingReportAsync(
        RuleReportKey ruleReportKey,
        string ruleKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            IReadOnlyList<StoredRuleReportIssue> snapshotIssues = _snapshotStore.InitializeAndGetSnapshot(
                ruleReportKey,
                ruleKey.Trim());
            _workingStateStore.Initialize(ruleFlowKey, snapshotIssues);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<StoredRuleReportIssue> AddAsync(
        RuleFlowKey ruleFlowKey,
        RuleReviewIssue issue,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(issue);
        NormalizedRuleIssue normalizedIssue = RuleIssueNormalizer.NormalizeToContract(issue);
        StoredRuleReportIssue generatedIssue = RuleIssueStoreMapper.CreateReportIssue(
            normalizedIssue,
            Guid.NewGuid().ToString("N"));

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.FromResult(generatedIssue);

            return ValueTask.FromResult(_workingStateStore.Add(
                ruleFlowKey,
                normalizedIssue,
                generatedIssue.RuleReportIssueId));
        }
    }

    public ValueTask<StoredRuleReportIssue> GetAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReportIssueId);

        lock (_syncRoot)
            return ValueTask.FromResult(_workingStateStore.Get(ruleFlowKey, ruleReportIssueId));
    }

    public ValueTask<IReadOnlyList<StoredRuleReportIssue>> ListAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_workingStateStore.List(ruleFlowKey));
    }

    public ValueTask<StoredRuleReportIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        RuleReviewIssue issue,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReportIssueId);
        ArgumentNullException.ThrowIfNull(issue);
        NormalizedRuleIssue normalizedIssue = RuleIssueNormalizer.NormalizeToContract(issue);

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.FromResult(_workingStateStore.Get(ruleFlowKey, ruleReportIssueId));

            return ValueTask.FromResult(_workingStateStore.Update(ruleFlowKey, ruleReportIssueId, normalizedIssue));
        }
    }

    public ValueTask<bool> DeleteAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReportIssueId);

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.FromResult(false);

            return ValueTask.FromResult(_workingStateStore.Delete(ruleFlowKey, ruleReportIssueId));
        }
    }

    public ValueTask<IReadOnlyList<StoredRuleReportIssue>> GetLatestSnapshotAsync(
        RuleReportKey ruleReportKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_snapshotStore.GetLatestSnapshot(ruleReportKey));
    }

    public ValueTask<RuleReportDiff> GetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_workingStateStore.GetLatestDiff(ruleFlowKey));
    }

    public ValueTask SetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        RuleReportDiff diff,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(diff);

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            _workingStateStore.SetLatestDiff(ruleFlowKey, diff);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask PromoteWorkingReportAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            _snapshotStore.Promote(ruleReportKey, _workingStateStore.List(ruleFlowKey));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearWorkingReportAsync(RuleFlowKey ruleFlowKey, CancellationToken _)
    {
        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            _workingStateStore.Clear(ruleFlowKey);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            _snapshotStore.Clear(ruleReportKey);
            _workingStateStore.Remove(ruleFlowKey);
            _writeGuard.Clear(ruleFlowKey);
        }

        return ValueTask.CompletedTask;
    }

    public IAgentAttemptLease BeginAttempt(RuleFlowKey ruleFlowKey, Guid attemptId)
    {
        lock (_syncRoot)
        {
            RuleReportFlowState? snapshot = _workingStateStore.Clone(ruleFlowKey);
            return _writeGuard.BeginAttempt(
                ruleFlowKey,
                attemptId,
                () => _workingStateStore.Restore(ruleFlowKey, snapshot));
        }
    }
}
