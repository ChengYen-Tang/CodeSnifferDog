using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report.State;

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
