using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report.State;

/// <summary>
/// Stores all mutable working state for one report aggregation flow.
/// </summary>
internal sealed class FlowState
{
    /// <summary>
    /// Gets the current working issues.
    /// </summary>
    public List<StoredIssue> WorkingIssues { get; } = [];

    /// <summary>
    /// Gets or sets the latest computed diff.
    /// </summary>
    public Diff LatestDiff { get; set; } = WorkingStateStore.CreateEmptyDiff();

    /// <summary>
    /// Clones the flow state.
    /// </summary>
    /// <returns>The cloned flow state.</returns>
    public FlowState Clone()
    {
        FlowState clone = new()
        {
            LatestDiff = WorkingStateStore.CloneDiff(LatestDiff),
        };
        clone.WorkingIssues.AddRange(WorkingIssues.Select(RuleIssueStoreMapper.Clone));
        return clone;
    }
}
