using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.Report;

internal sealed class RuleReportIssueToolService(
    IRuleReportIssueStore reportIssueStore,
    RuleFlowKey ruleFlowKey)
{
    private readonly IRuleReportIssueStore _reportIssueStore = reportIssueStore;
    private readonly RuleFlowKey _ruleFlowKey = ruleFlowKey;

    public ValueTask<StoredRuleReportIssue> GetRuleReportIssueAsync(
        GetRuleReportIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReportIssueId);
        return _reportIssueStore.GetAsync(_ruleFlowKey, args.RuleReportIssueId.Trim(), cancellationToken);
    }

    public ValueTask<IReadOnlyList<StoredRuleReportIssue>> ListRuleReportIssuesAsync(CancellationToken cancellationToken)
        =>
        _reportIssueStore.ListAsync(_ruleFlowKey, cancellationToken);

    public async ValueTask<CreateRuleReportIssueResult> CreateRuleReportIssueAsync(
        CreateRuleReportIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        StoredRuleReportIssue issue = await _reportIssueStore.AddAsync(
            _ruleFlowKey,
            CreateIssue(args),
            cancellationToken).ConfigureAwait(false);

        return new CreateRuleReportIssueResult
        {
            RuleReportIssueId = issue.RuleReportIssueId,
        };
    }

    public ValueTask<StoredRuleReportIssue> UpdateRuleReportIssueAsync(
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

    public ValueTask<RuleReportDiff> GetLatestDiffAsync(CancellationToken cancellationToken)
        =>
        _reportIssueStore.GetLatestDiffAsync(_ruleFlowKey, cancellationToken);

    public ValueTask SetLatestDiffAsync(RuleReportDiff diff, CancellationToken cancellationToken)
        =>
        _reportIssueStore.SetLatestDiffAsync(_ruleFlowKey, diff, cancellationToken);

    private static RuleReviewIssue CreateIssue(CreateRuleReportIssueArgs args) =>
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

    private static RuleReviewIssue CreateIssue(UpdateRuleReportIssueArgs args) =>
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
