using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report.State;

internal sealed class SnapshotState(string ruleKey)
{
    public string RuleKey { get; } = ruleKey;

    public List<StoredIssue> Issues { get; } = [];

    public SnapshotState Clone()
    {
        SnapshotState clone = new(RuleKey);
        clone.Issues.AddRange(Issues.Select(RuleIssueStoreMapper.Clone));
        return clone;
    }
}
