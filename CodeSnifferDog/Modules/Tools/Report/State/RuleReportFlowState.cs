using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report.State;

internal sealed class RuleReportFlowState
{
    public List<StoredRuleReportIssue> WorkingIssues { get; } = [];

    public RuleReportDiff LatestDiff { get; set; } = RuleReportWorkingStateStore.CreateEmptyDiff();

    public RuleReportFlowState Clone()
    {
        RuleReportFlowState clone = new()
        {
            LatestDiff = RuleReportWorkingStateStore.CloneDiff(LatestDiff),
        };
        clone.WorkingIssues.AddRange(WorkingIssues.Select(RuleIssueStoreMapper.Clone));
        return clone;
    }
}
