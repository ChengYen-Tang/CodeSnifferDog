using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report.State;

/// <summary>
/// Stores the promoted snapshot for one rule report.
/// </summary>
internal sealed class SnapshotState(string ruleKey)
{
    /// <summary>
    /// Gets the rule key associated with the snapshot.
    /// </summary>
    public string RuleKey { get; } = ruleKey;

    /// <summary>
    /// Gets the stored snapshot issues.
    /// </summary>
    public List<StoredIssue> Issues { get; } = [];

    /// <summary>
    /// Clones the snapshot state.
    /// </summary>
    /// <returns>The cloned snapshot state.</returns>
    public SnapshotState Clone()
    {
        SnapshotState clone = new(RuleKey);
        clone.Issues.AddRange(Issues.Select(RuleIssueStoreMapper.Clone));
        return clone;
    }
}
