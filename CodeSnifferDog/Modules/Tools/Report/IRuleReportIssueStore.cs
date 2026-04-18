using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.Report;

public interface IRuleReportIssueStore
{
    ValueTask InitializeWorkingReportAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    ValueTask<StoredRuleReportIssue> AddAsync(
        RuleFlowKey ruleFlowKey,
        RuleReviewIssue issue,
        CancellationToken cancellationToken);

    ValueTask<StoredRuleReportIssue> GetAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredRuleReportIssue>> ListAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    ValueTask<StoredRuleReportIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        RuleReviewIssue issue,
        CancellationToken cancellationToken);

    ValueTask<bool> DeleteAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredRuleReportIssue>> GetLatestSnapshotAsync(
        RuleReportKey ruleReportKey,
        CancellationToken cancellationToken);

    ValueTask<RuleReportDiff> GetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    ValueTask SetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        RuleReportDiff diff,
        CancellationToken cancellationToken);

    ValueTask PromoteWorkingReportAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    ValueTask ClearWorkingReportAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);

    ValueTask ClearAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);
}
