using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Modules.Tools.Report;

internal sealed class IssueToolService(
    IIssueStore reportIssueStore,
    RuleFlowKey ruleFlowKey)
{
    private readonly IIssueStore _reportIssueStore = reportIssueStore;
    private readonly RuleFlowKey _ruleFlowKey = ruleFlowKey;

    public ValueTask<ReportStoredIssue> GetRuleReportIssueAsync(
        GetRuleReportIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReportIssueId);
        return _reportIssueStore.GetAsync(_ruleFlowKey, args.RuleReportIssueId.Trim(), cancellationToken);
    }

    public ValueTask<IReadOnlyList<ReportStoredIssue>> ListRuleReportIssuesAsync(CancellationToken cancellationToken)
        =>
        _reportIssueStore.ListAsync(_ruleFlowKey, cancellationToken);

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

    public ValueTask<ReportStoredIssue> UpdateRuleReportIssueAsync(
        UpdateRuleReportIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReportIssueId);
        return _reportIssueStore.UpdateAsync(_ruleFlowKey, args.RuleReportIssueId.Trim(), CreateIssue(args), cancellationToken);
    }

    public ValueTask<bool> DeleteRuleReportIssueAsync(
        DeleteRuleReportIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReportIssueId);
        return _reportIssueStore.DeleteAsync(_ruleFlowKey, args.RuleReportIssueId.Trim(), cancellationToken);
    }

    public ValueTask<Diff> GetLatestDiffAsync(CancellationToken cancellationToken)
        =>
        _reportIssueStore.GetLatestDiffAsync(_ruleFlowKey, cancellationToken);

    public ValueTask SetLatestDiffAsync(Diff diff, CancellationToken cancellationToken)
        =>
        _reportIssueStore.SetLatestDiffAsync(_ruleFlowKey, diff, cancellationToken);

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
