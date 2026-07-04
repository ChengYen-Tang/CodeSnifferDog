using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Attempts;
using CodeSnifferDog.Modules.Tools.Issues;
using CodeSnifferDog.Modules.Tools.Report.State;
using CodeSnifferDog.Workflows.Common;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Modules.Tools.Report;

/// <summary>
/// Stores repository-level report issues in memory with retry-safe rollback support.
/// </summary>
public sealed class InMemoryIssueStore : IIssueStore
{
    private readonly SnapshotStore _snapshotStore = new();
    private readonly WorkingStateStore _workingStateStore = new();
    private readonly ScopedAttemptWriteGuard<RuleFlowKey> _writeGuard = new();
    private readonly Lock _syncRoot = new();

    /// <inheritdoc />
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

            IReadOnlyList<ReportStoredIssue> snapshotIssues = _snapshotStore.InitializeAndGetSnapshot(
                ruleReportKey,
                ruleKey.Trim());
            _workingStateStore.Initialize(ruleFlowKey, snapshotIssues);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ReportStoredIssue> AddAsync(
        RuleFlowKey ruleFlowKey,
        Issue issue,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(issue);
        NormalizedRuleIssue normalizedIssue = RuleIssueNormalizer.NormalizeToContract(issue);
        ReportStoredIssue generatedIssue = RuleIssueStoreMapper.CreateReportIssue(
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

    /// <inheritdoc />
    public ValueTask<ReportStoredIssue> GetAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReportIssueId);

        lock (_syncRoot)
            return ValueTask.FromResult(_workingStateStore.Get(ruleFlowKey, ruleReportIssueId));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ReportStoredIssue>> ListAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_workingStateStore.List(ruleFlowKey));
    }

    /// <inheritdoc />
    public ValueTask<ReportStoredIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        Issue issue,
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ReportStoredIssue>> GetLatestSnapshotAsync(
        RuleReportKey ruleReportKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_snapshotStore.GetLatestSnapshot(ruleReportKey));
    }

    /// <inheritdoc />
    public ValueTask<Diff> GetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_workingStateStore.GetLatestDiff(ruleFlowKey));
    }

    /// <inheritdoc />
    public ValueTask SetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        Diff diff,
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public IAgentAttemptLease BeginAttempt(RuleFlowKey ruleFlowKey, Guid attemptId)
    {
        lock (_syncRoot)
        {
            FlowState? snapshot = _workingStateStore.Clone(ruleFlowKey);
            return _writeGuard.BeginAttempt(
                ruleFlowKey,
                attemptId,
                () => _workingStateStore.Restore(ruleFlowKey, snapshot));
        }
    }
}
