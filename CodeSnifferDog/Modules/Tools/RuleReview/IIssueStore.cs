using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

public interface IIssueStore : CodeSnifferDog.Workflows.Common.IScopedRetrySafeAgentStore<RuleFlowKey>
{
    ValueTask<StoredIssue> AddAsync(RuleFlowKey ruleFlowKey, Issue issue, CancellationToken cancellationToken);

    ValueTask<StoredIssue> GetAsync(RuleFlowKey ruleFlowKey, string ruleReviewIssueId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredIssue>> ListAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);

    ValueTask<StoredIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReviewIssueId,
        Issue issue,
        CancellationToken cancellationToken);

    ValueTask<bool> DeleteAsync(RuleFlowKey ruleFlowKey, string ruleReviewIssueId, CancellationToken cancellationToken);

    ValueTask<NoIssueConclusion?> GetNoIssueConclusionAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);

    ValueTask SubmitNoIssueConclusionAsync(
        RuleFlowKey ruleFlowKey,
        NoIssueConclusion conclusion,
        CancellationToken cancellationToken);

    ValueTask ClearAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);
}
