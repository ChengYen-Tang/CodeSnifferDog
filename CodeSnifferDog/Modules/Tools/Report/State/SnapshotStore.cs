using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report.State;

internal sealed class SnapshotStore
{
    private readonly Dictionary<RuleReportKey, SnapshotState> _latestSnapshots = [];

    public IReadOnlyList<StoredIssue> InitializeAndGetSnapshot(RuleReportKey ruleReportKey, string ruleKey) =>
        [.. GetOrCreateLatestSnapshot(ruleReportKey, ruleKey).Issues];

    public IReadOnlyList<StoredIssue> GetLatestSnapshot(RuleReportKey ruleReportKey) =>
        _latestSnapshots.TryGetValue(ruleReportKey, out SnapshotState? state) ? [.. state.Issues] : [];

    public void Promote(RuleReportKey ruleReportKey, IReadOnlyList<StoredIssue> workingIssues)
    {
        SnapshotState latestSnapshot = GetOrCreateLatestSnapshot(ruleReportKey, null);
        latestSnapshot.Issues.Clear();

        foreach (StoredIssue issue in workingIssues)
            latestSnapshot.Issues.Add(RuleIssueStoreMapper.Clone(issue));
    }

    public void Clear(RuleReportKey ruleReportKey) =>
        _latestSnapshots.Remove(ruleReportKey);

    private SnapshotState GetOrCreateLatestSnapshot(RuleReportKey ruleReportKey, string? ruleKey)
    {
        if (_latestSnapshots.TryGetValue(ruleReportKey, out SnapshotState? state))
        {
            if (!string.IsNullOrWhiteSpace(ruleKey) &&
                !string.Equals(state.RuleKey, ruleKey.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException($"Rule key mismatch for report key '{ruleReportKey}'.");

            return state;
        }

        if (string.IsNullOrWhiteSpace(ruleKey))
            throw new KeyNotFoundException($"Rule report snapshot was not initialized: {ruleReportKey}");

        state = new SnapshotState(ruleKey.Trim());
        _latestSnapshots.Add(ruleReportKey, state);
        return state;
    }
}
