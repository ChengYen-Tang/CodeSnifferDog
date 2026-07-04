using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.RuleReview.State;

/// <summary>
/// Stores all mutable state for one rule-review flow.
/// </summary>
internal sealed class FlowState
{
    /// <summary>
    /// Gets the stored issues for the flow.
    /// </summary>
    public List<StoredIssue> Issues { get; } = [];

    /// <summary>
    /// Gets or sets the no-issue conclusion for the flow.
    /// </summary>
    public NoIssueConclusion? NoIssueConclusion { get; set; }

    /// <summary>
    /// Clones the flow state.
    /// </summary>
    /// <returns>The cloned flow state.</returns>
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
