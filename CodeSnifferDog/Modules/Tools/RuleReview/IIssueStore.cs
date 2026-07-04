using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

/// <summary>
/// Stores rule-review issues for one rule flow with retry-safe rollback support.
/// </summary>
public interface IIssueStore : CodeSnifferDog.Workflows.Common.IScopedRetrySafeAgentStore<RuleFlowKey>
{
    /// <summary>
    /// Adds one issue to the specified rule flow.
    /// </summary>
    ValueTask<StoredIssue> AddAsync(RuleFlowKey ruleFlowKey, Issue issue, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one stored issue by identifier.
    /// </summary>
    ValueTask<StoredIssue> GetAsync(RuleFlowKey ruleFlowKey, string ruleReviewIssueId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the stored issues for the specified rule flow.
    /// </summary>
    ValueTask<IReadOnlyList<StoredIssue>> ListAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);

    /// <summary>
    /// Updates one stored issue by identifier.
    /// </summary>
    ValueTask<StoredIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReviewIssueId,
        Issue issue,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one stored issue by identifier.
    /// </summary>
    ValueTask<bool> DeleteAsync(RuleFlowKey ruleFlowKey, string ruleReviewIssueId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the submitted no-issue conclusion, if one exists.
    /// </summary>
    ValueTask<NoIssueConclusion?> GetNoIssueConclusionAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);

    /// <summary>
    /// Submits a no-issue conclusion for the specified rule flow.
    /// </summary>
    ValueTask SubmitNoIssueConclusionAsync(
        RuleFlowKey ruleFlowKey,
        NoIssueConclusion conclusion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clears all stored state for the specified rule flow.
    /// </summary>
    ValueTask ClearAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);
}
