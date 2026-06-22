using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report.State;

internal sealed class RuleReportSnapshotStore
{
    private readonly Dictionary<RuleReportKey, RuleReportSnapshotState> _latestSnapshots = [];

    public IReadOnlyList<StoredRuleReportIssue> InitializeAndGetSnapshot(RuleReportKey ruleReportKey, string ruleKey) =>
        [.. GetOrCreateLatestSnapshot(ruleReportKey, ruleKey).Issues];

    public IReadOnlyList<StoredRuleReportIssue> GetLatestSnapshot(RuleReportKey ruleReportKey) =>
        _latestSnapshots.TryGetValue(ruleReportKey, out RuleReportSnapshotState? state) ? [.. state.Issues] : [];

    public void Promote(RuleReportKey ruleReportKey, IReadOnlyList<StoredRuleReportIssue> workingIssues)
    {
        RuleReportSnapshotState latestSnapshot = GetOrCreateLatestSnapshot(ruleReportKey, null);
        latestSnapshot.Issues.Clear();

        foreach (StoredRuleReportIssue issue in workingIssues)
            latestSnapshot.Issues.Add(RuleIssueStoreMapper.Clone(issue));
    }

    public void Clear(RuleReportKey ruleReportKey) =>
        _latestSnapshots.Remove(ruleReportKey);

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
}
