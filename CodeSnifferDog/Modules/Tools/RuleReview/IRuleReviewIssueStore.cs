using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

public interface IRuleReviewIssueStore
{
    ValueTask<StoredRuleReviewIssue> AddAsync(RuleReviewIssue issue, CancellationToken cancellationToken);

    ValueTask<StoredRuleReviewIssue> GetAsync(string ruleReviewIssueId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredRuleReviewIssue>> ListAsync(CancellationToken cancellationToken);

    ValueTask<StoredRuleReviewIssue> UpdateAsync(
        string ruleReviewIssueId,
        RuleReviewIssue issue,
        CancellationToken cancellationToken);

    ValueTask<bool> DeleteAsync(string ruleReviewIssueId, CancellationToken cancellationToken);

    ValueTask<NoIssueConclusion?> GetNoIssueConclusionAsync(CancellationToken cancellationToken);

    ValueTask SubmitNoIssueConclusionAsync(NoIssueConclusion conclusion, CancellationToken cancellationToken);

    ValueTask ClearAsync(CancellationToken cancellationToken);
}
