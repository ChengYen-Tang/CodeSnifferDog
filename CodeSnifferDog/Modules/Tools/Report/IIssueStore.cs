using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Modules.Tools.Report;

public interface IIssueStore : CodeSnifferDog.Workflows.Common.IScopedRetrySafeAgentStore<RuleFlowKey>
{
    ValueTask InitializeWorkingReportAsync(
        RuleReportKey ruleReportKey,
        string ruleKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    ValueTask<ReportStoredIssue> AddAsync(
        RuleFlowKey ruleFlowKey,
        Issue issue,
        CancellationToken cancellationToken);

    ValueTask<ReportStoredIssue> GetAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ReportStoredIssue>> ListAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    ValueTask<ReportStoredIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        Issue issue,
        CancellationToken cancellationToken);

    ValueTask<bool> DeleteAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ReportStoredIssue>> GetLatestSnapshotAsync(
        RuleReportKey ruleReportKey,
        CancellationToken cancellationToken);

    ValueTask<Diff> GetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    ValueTask SetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        Diff diff,
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
