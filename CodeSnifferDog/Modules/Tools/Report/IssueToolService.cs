using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Modules.Tools.Report;

/// <summary>
/// Validates report tool arguments and delegates issue operations to the report issue store.
/// </summary>
internal sealed class IssueToolService(
    IIssueStore reportIssueStore,
    RuleFlowKey ruleFlowKey)
{
    private readonly IIssueStore _reportIssueStore = reportIssueStore;
    private readonly RuleFlowKey _ruleFlowKey = ruleFlowKey;

    /// <summary>
    /// Gets one stored repository-level issue.
    /// </summary>
    public ValueTask<ReportStoredIssue> GetRuleReportIssueAsync(
        GetRuleReportIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReportIssueId);
        return _reportIssueStore.GetAsync(_ruleFlowKey, args.RuleReportIssueId.Trim(), cancellationToken);
    }

    /// <summary>
    /// Lists the repository-level issues in the working report.
    /// </summary>
    public ValueTask<IReadOnlyList<ReportStoredIssue>> ListRuleReportIssuesAsync(CancellationToken cancellationToken)
        =>
        _reportIssueStore.ListAsync(_ruleFlowKey, cancellationToken);

    /// <summary>
    /// Creates one stored repository-level issue.
    /// </summary>
    public async ValueTask<CreateRuleReportIssueResult> CreateRuleReportIssueAsync(
        CreateRuleReportIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ReportStoredIssue issue = await _reportIssueStore.AddAsync(
            _ruleFlowKey,
            CreateIssue(args),
            cancellationToken).ConfigureAwait(false);

        return new CreateRuleReportIssueResult
        {
            RuleReportIssueId = issue.RuleReportIssueId,
        };
    }

    /// <summary>
    /// Updates one stored repository-level issue.
    /// </summary>
    public ValueTask<ReportStoredIssue> UpdateRuleReportIssueAsync(
        UpdateRuleReportIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReportIssueId);
        return _reportIssueStore.UpdateAsync(_ruleFlowKey, args.RuleReportIssueId.Trim(), CreateIssue(args), cancellationToken);
    }

    /// <summary>
    /// Deletes one stored repository-level issue.
    /// </summary>
    public ValueTask<bool> DeleteRuleReportIssueAsync(
        DeleteRuleReportIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReportIssueId);
        return _reportIssueStore.DeleteAsync(_ruleFlowKey, args.RuleReportIssueId.Trim(), cancellationToken);
    }

    /// <summary>
    /// Gets the latest diff for the working report.
    /// </summary>
    public ValueTask<Diff> GetLatestDiffAsync(CancellationToken cancellationToken)
        =>
        _reportIssueStore.GetLatestDiffAsync(_ruleFlowKey, cancellationToken);

    /// <summary>
    /// Stores the latest diff for the working report.
    /// </summary>
    public ValueTask SetLatestDiffAsync(Diff diff, CancellationToken cancellationToken)
        =>
        _reportIssueStore.SetLatestDiffAsync(_ruleFlowKey, diff, cancellationToken);

    /// <summary>
    /// Creates a normalized issue from create arguments.
    /// </summary>
    private static Issue CreateIssue(CreateRuleReportIssueArgs args) =>
        RuleIssueNormalizer.Create(
            args.IssueType,
            args.Severity,
            args.FileOrFunction,
            args.RelevantCodePatternOrExpression,
            args.WhyThisIsAProblem,
            args.Confidence,
            args.FollowUpFiles,
            args.SuggestedFixDirection,
            args.ScopeCoverage,
            args.CrossScopeAnalysis,
            args.ReviewStrategy);

    /// <summary>
    /// Creates a normalized issue from update arguments.
    /// </summary>
    private static Issue CreateIssue(UpdateRuleReportIssueArgs args) =>
        RuleIssueNormalizer.Create(
            args.IssueType,
            args.Severity,
            args.FileOrFunction,
            args.RelevantCodePatternOrExpression,
            args.WhyThisIsAProblem,
            args.Confidence,
            args.FollowUpFiles,
            args.SuggestedFixDirection,
            args.ScopeCoverage,
            args.CrossScopeAnalysis,
            args.ReviewStrategy);
}
