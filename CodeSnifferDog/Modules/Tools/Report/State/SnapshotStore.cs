using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report.State;

/// <summary>
/// Stores the latest promoted report snapshot for each rule report key.
/// </summary>
internal sealed class SnapshotStore
{
    private readonly Dictionary<RuleReportKey, SnapshotState> _latestSnapshots = [];

    /// <summary>
    /// Ensures a snapshot exists and returns its stored issues.
    /// </summary>
    public IReadOnlyList<StoredIssue> InitializeAndGetSnapshot(RuleReportKey ruleReportKey, string ruleKey) =>
        [.. GetOrCreateLatestSnapshot(ruleReportKey, ruleKey).Issues];

    /// <summary>
    /// Gets the latest promoted snapshot for a rule report key.
    /// </summary>
    public IReadOnlyList<StoredIssue> GetLatestSnapshot(RuleReportKey ruleReportKey) =>
        _latestSnapshots.TryGetValue(ruleReportKey, out SnapshotState? state) ? [.. state.Issues] : [];

    /// <summary>
    /// Promotes the working issues into the latest snapshot.
    /// </summary>
    public void Promote(RuleReportKey ruleReportKey, IReadOnlyList<StoredIssue> workingIssues)
    {
        SnapshotState latestSnapshot = GetOrCreateLatestSnapshot(ruleReportKey, null);
        latestSnapshot.Issues.Clear();

        foreach (StoredIssue issue in workingIssues)
            latestSnapshot.Issues.Add(RuleIssueStoreMapper.Clone(issue));
    }

    /// <summary>
    /// Clears the latest snapshot for a rule report key.
    /// </summary>
    public void Clear(RuleReportKey ruleReportKey) =>
        _latestSnapshots.Remove(ruleReportKey);

    /// <summary>
    /// Gets the existing latest snapshot or creates a new one.
    /// </summary>
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
