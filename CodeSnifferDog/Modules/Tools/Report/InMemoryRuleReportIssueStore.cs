using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Report;

public sealed class InMemoryRuleReportIssueStore : IRuleReportIssueStore
{
    private readonly Dictionary<RuleReportKey, RuleReportSnapshotState> _latestSnapshots = [];
    private readonly Dictionary<RuleFlowKey, RuleReportFlowState> _flowStates = [];
    private readonly Dictionary<RuleFlowKey, Guid> _activeAttemptIds = [];
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
            if (!CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            RuleReportSnapshotState latestSnapshot = GetOrCreateLatestSnapshot(ruleReportKey, ruleKey.Trim());
            RuleReportFlowState flowState = GetOrCreateFlowState(ruleFlowKey);
            flowState.WorkingIssues.Clear();

            foreach (StoredRuleReportIssue issue in latestSnapshot.Issues)
                flowState.WorkingIssues.Add(RuleIssueStoreMapper.Clone(issue));

            flowState.LatestDiff = CreateEmptyDiff();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<StoredRuleReportIssue> AddAsync(
        RuleFlowKey ruleFlowKey,
        RuleReviewIssue issue,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(issue);
        StoredRuleReportIssue storedIssue = RuleIssueStoreMapper.CreateReportIssue(
            NormalizeIssue(issue),
            Guid.NewGuid().ToString("N"));

        lock (_syncRoot)
        {
            if (!CanWrite(ruleFlowKey))
                return ValueTask.FromResult(storedIssue);

            RuleReportFlowState flowState = GetOrCreateFlowState(ruleFlowKey);
            StoredRuleReportIssue? existingIssue = flowState.WorkingIssues
                .FirstOrDefault(candidate => RuleIssueStoreMapper.IsEquivalentToIssue(candidate, issue));
            if (existingIssue is not null)
                return ValueTask.FromResult(existingIssue);

            flowState.WorkingIssues.Add(storedIssue);
        }

        return ValueTask.FromResult(storedIssue);
    }

    public ValueTask<StoredRuleReportIssue> GetAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReportIssueId);

        lock (_syncRoot)
        {
            return ValueTask.FromResult(
                GetOrCreateFlowState(ruleFlowKey).WorkingIssues
                    .FirstOrDefault(item => item.RuleReportIssueId == ruleReportIssueId.Trim())
                ?? throw new KeyNotFoundException($"Rule report issue was not found: {ruleReportIssueId}"));
        }
    }

    public ValueTask<IReadOnlyList<StoredRuleReportIssue>> ListAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<IReadOnlyList<StoredRuleReportIssue>>([.. GetOrCreateFlowState(ruleFlowKey).WorkingIssues]);
    }

    public ValueTask<StoredRuleReportIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        RuleReviewIssue issue,
        CancellationToken _)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleReportIssueId);
        ArgumentNullException.ThrowIfNull(issue);
        RuleReviewIssue normalizedIssue = NormalizeIssue(issue);

        lock (_syncRoot)
        {
            if (!CanWrite(ruleFlowKey))
            {
                RuleReportFlowState existingState = GetOrCreateFlowState(ruleFlowKey);
                StoredRuleReportIssue existingIssue = existingState.WorkingIssues
                    .FirstOrDefault(item => item.RuleReportIssueId == ruleReportIssueId.Trim())
                    ?? throw new KeyNotFoundException($"Rule report issue was not found: {ruleReportIssueId}");
                return ValueTask.FromResult(existingIssue);
            }

            RuleReportFlowState flowState = GetOrCreateFlowState(ruleFlowKey);
            int index = flowState.WorkingIssues.FindIndex(item => item.RuleReportIssueId == ruleReportIssueId.Trim());

            if (index < 0)
                throw new KeyNotFoundException($"Rule report issue was not found: {ruleReportIssueId}");

            StoredRuleReportIssue storedIssue = RuleIssueStoreMapper.CreateReportIssue(
                normalizedIssue,
                flowState.WorkingIssues[index].RuleReportIssueId);
            flowState.WorkingIssues[index] = storedIssue;
            return ValueTask.FromResult(storedIssue);
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
            if (!CanWrite(ruleFlowKey))
                return ValueTask.FromResult(false);

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
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<IReadOnlyList<StoredRuleReportIssue>>(
                _latestSnapshots.TryGetValue(ruleReportKey, out RuleReportSnapshotState? state) ? [.. state.Issues] : []);
    }

    public ValueTask<RuleReportDiff> GetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken _)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(GetOrCreateFlowState(ruleFlowKey).LatestDiff);
    }

    public ValueTask SetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        RuleReportDiff diff,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(diff);

        lock (_syncRoot)
        {
            if (!CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            GetOrCreateFlowState(ruleFlowKey).LatestDiff = diff;
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
            if (!CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            RuleReportSnapshotState latestSnapshot = GetOrCreateLatestSnapshot(ruleReportKey, null);
            RuleReportFlowState flowState = GetOrCreateFlowState(ruleFlowKey);
            latestSnapshot.Issues.Clear();

            foreach (StoredRuleReportIssue issue in flowState.WorkingIssues)
                latestSnapshot.Issues.Add(RuleIssueStoreMapper.Clone(issue));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearWorkingReportAsync(RuleFlowKey ruleFlowKey, CancellationToken _)
    {
        lock (_syncRoot)
        {
            if (!CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

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
        CancellationToken _)
    {
        lock (_syncRoot)
        {
            if (!CanWrite(ruleFlowKey))
                return ValueTask.CompletedTask;

            _latestSnapshots.Remove(ruleReportKey);
            _flowStates.Remove(ruleFlowKey);
            _activeAttemptIds.Remove(ruleFlowKey);
        }

        return ValueTask.CompletedTask;
    }

    public IAgentAttemptLease BeginAttempt(RuleFlowKey ruleFlowKey, Guid attemptId)
    {
        lock (_syncRoot)
        {
            _flowStates.TryGetValue(ruleFlowKey, out RuleReportFlowState? previousFlowState);
            Guid staleWriteBlockerAttemptId = Guid.NewGuid();
            RuleReportFlowState? snapshot = previousFlowState?.Clone();
            _activeAttemptIds[ruleFlowKey] = attemptId;

            return new AgentAttemptLease(() =>
            {
                lock (_syncRoot)
                {
                    _activeAttemptIds[ruleFlowKey] = staleWriteBlockerAttemptId;

                    if (snapshot is null)
                        _flowStates.Remove(ruleFlowKey);
                    else
                        _flowStates[ruleFlowKey] = snapshot.Clone();
                }
            });
        }
    }

    private RuleReportSnapshotState GetOrCreateLatestSnapshot(RuleReportKey ruleReportKey, string? ruleKey)
    {
        if (_latestSnapshots.TryGetValue(ruleReportKey, out RuleReportSnapshotState? state))
        {
            if (!string.IsNullOrWhiteSpace(ruleKey) &&
                !string.Equals(state.RuleKey, ruleKey.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException($"Rule key mismatch for report key '{ruleReportKey}'.");

            return state;
        }

        if (string.IsNullOrWhiteSpace(ruleKey))
            throw new KeyNotFoundException($"Rule report snapshot was not initialized: {ruleReportKey}");

        state = new RuleReportSnapshotState(ruleKey.Trim());
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

    private static RuleReviewIssue NormalizeIssue(RuleReviewIssue issue) =>
        RuleIssueNormalizer.Normalize(issue);

    private bool CanWrite(RuleFlowKey ruleFlowKey)
    {
        Guid? currentAttemptId = AgentRunAttemptContext.CurrentAttemptId;
        return currentAttemptId is null ||
            !_activeAttemptIds.TryGetValue(ruleFlowKey, out Guid activeAttemptId) ||
            currentAttemptId == activeAttemptId;
    }

    internal sealed class RuleReportFlowState
    {
        public List<StoredRuleReportIssue> WorkingIssues { get; } = [];

        public RuleReportDiff LatestDiff { get; set; } = CreateEmptyDiff();

        public RuleReportFlowState Clone()
        {
            RuleReportFlowState clone = new()
            {
                LatestDiff = new RuleReportDiff
                {
                    CreatedIssues = [.. LatestDiff.CreatedIssues.Select(RuleIssueStoreMapper.Clone)],
                    UpdatedIssues = [.. LatestDiff.UpdatedIssues.Select(RuleIssueStoreMapper.Clone)],
                    DeletedIssues = [.. LatestDiff.DeletedIssues.Select(RuleIssueStoreMapper.Clone)],
                },
            };
            clone.WorkingIssues.AddRange(WorkingIssues.Select(RuleIssueStoreMapper.Clone));
            return clone;
        }
    }

    internal sealed class RuleReportSnapshotState(string ruleKey)
    {
        public string RuleKey { get; } = ruleKey;

        public List<StoredRuleReportIssue> Issues { get; } = [];

        public RuleReportSnapshotState Clone()
        {
            RuleReportSnapshotState clone = new(RuleKey);
            clone.Issues.AddRange(Issues.Select(RuleIssueStoreMapper.Clone));
            return clone;
        }
    }
}
