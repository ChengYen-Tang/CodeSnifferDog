using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report.State;

internal sealed class FlowState
{
    public List<StoredIssue> WorkingIssues { get; } = [];

    public Diff LatestDiff { get; set; } = WorkingStateStore.CreateEmptyDiff();

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
