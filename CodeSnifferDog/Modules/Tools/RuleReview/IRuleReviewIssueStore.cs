using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

public interface IRuleReviewIssueStore : CodeSnifferDog.Workflows.Common.IScopedRetrySafeAgentStore<RuleFlowKey>
{
    ValueTask<StoredRuleReviewIssue> AddAsync(RuleFlowKey ruleFlowKey, RuleReviewIssue issue, CancellationToken cancellationToken);

    ValueTask<StoredRuleReviewIssue> GetAsync(RuleFlowKey ruleFlowKey, string ruleReviewIssueId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredRuleReviewIssue>> ListAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);

    ValueTask<StoredRuleReviewIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReviewIssueId,
        RuleReviewIssue issue,
        CancellationToken cancellationToken);

    ValueTask<bool> DeleteAsync(RuleFlowKey ruleFlowKey, string ruleReviewIssueId, CancellationToken cancellationToken);

    ValueTask<NoIssueConclusion?> GetNoIssueConclusionAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);

    ValueTask SubmitNoIssueConclusionAsync(
        RuleFlowKey ruleFlowKey,
        NoIssueConclusion conclusion,
        CancellationToken cancellationToken);

    ValueTask ClearAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);
}
