using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Modules.Tools.Report;

/// <summary>
/// Stores repository-level report issues and snapshots for one rule flow.
/// </summary>
public interface IIssueStore : CodeSnifferDog.Workflows.Common.IScopedRetrySafeAgentStore<RuleFlowKey>
{
    /// <summary>
    /// Initializes the working report from the latest promoted snapshot.
    /// </summary>
    ValueTask InitializeWorkingReportAsync(
        RuleReportKey ruleReportKey,
        string ruleKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds one repository-level issue to the working report.
    /// </summary>
    ValueTask<ReportStoredIssue> AddAsync(
        RuleFlowKey ruleFlowKey,
        Issue issue,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one stored repository-level issue by identifier.
    /// </summary>
    ValueTask<ReportStoredIssue> GetAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all repository-level issues in the working report for internal aggregation and diff processing.
    /// This operation is not exposed as an agent tool.
    /// </summary>
    ValueTask<IReadOnlyList<ReportStoredIssue>> ListAllAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists at most <paramref name="take"/> repository-level issues after <paramref name="cursor"/>.
    /// </summary>
    ValueTask<IReadOnlyList<ReportStoredIssue>> ListPageAsync(
        RuleFlowKey ruleFlowKey,
        string? cursor,
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates one stored repository-level issue by identifier.
    /// </summary>
    ValueTask<ReportStoredIssue> UpdateAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        Issue issue,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one stored repository-level issue by identifier.
    /// </summary>
    ValueTask<bool> DeleteAsync(
        RuleFlowKey ruleFlowKey,
        string ruleReportIssueId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the latest promoted snapshot for a report key.
    /// </summary>
    ValueTask<IReadOnlyList<ReportStoredIssue>> GetLatestSnapshotAsync(
        RuleReportKey ruleReportKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the latest diff for the working report.
    /// </summary>
    ValueTask<Diff> GetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores the latest diff for the working report.
    /// </summary>
    ValueTask SetLatestDiffAsync(
        RuleFlowKey ruleFlowKey,
        Diff diff,
        CancellationToken cancellationToken);

    /// <summary>
    /// Promotes the current working report into the latest snapshot.
    /// </summary>
    ValueTask PromoteWorkingReportAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clears the working report for a rule flow.
    /// </summary>
    ValueTask ClearWorkingReportAsync(RuleFlowKey ruleFlowKey, CancellationToken cancellationToken);

    /// <summary>
    /// Clears both snapshot and working state for a report key and rule flow.
    /// </summary>
    ValueTask ClearAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken);
}
