using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.RuleReview.State;

internal sealed class FlowState
{
    public List<StoredIssue> Issues { get; } = [];

    public NoIssueConclusion? NoIssueConclusion { get; set; }

    public FlowState Clone()
    {
        FlowState clone = new()
        {
            NoIssueConclusion = NoIssueConclusion is null
                ? null
                : new NoIssueConclusion
                {
                    ReviewStrategy = NoIssueConclusion.ReviewStrategy,
                    ScopeCoverage = NoIssueConclusion.ScopeCoverage,
                    CrossScopeAnalysis = NoIssueConclusion.CrossScopeAnalysis,
                    WhyNoIssueWasFound = NoIssueConclusion.WhyNoIssueWasFound,
                },
        };

        clone.Issues.AddRange(Issues.Select(RuleIssueStoreMapper.Clone));
        return clone;
    }
}
