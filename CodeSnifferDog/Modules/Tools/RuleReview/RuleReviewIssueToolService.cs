using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.RuleReview.Tools;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

internal sealed class RuleReviewIssueToolService(
    IRuleReviewIssueStore issueStore,
    RuleFlowKey ruleFlowKey)
{
    private readonly IRuleReviewIssueStore _issueStore = issueStore;
    private readonly RuleFlowKey _ruleFlowKey = ruleFlowKey;

    public async ValueTask<CreateRuleReviewIssueResult> CreateRuleReviewIssueAsync(
        CreateRuleReviewIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        StoredRuleReviewIssue issue = await _issueStore.AddAsync(
            _ruleFlowKey,
            CreateIssue(args),
            cancellationToken).ConfigureAwait(false);

        return new CreateRuleReviewIssueResult
        {
            RuleReviewIssueId = issue.RuleReviewIssueId,
        };
    }

    public ValueTask<StoredRuleReviewIssue> GetRuleReviewIssueAsync(
        GetRuleReviewIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReviewIssueId);
        return _issueStore.GetAsync(_ruleFlowKey, args.RuleReviewIssueId.Trim(), cancellationToken);
    }

    public ValueTask<IReadOnlyList<StoredRuleReviewIssue>> ListRuleReviewIssuesAsync(CancellationToken cancellationToken)
        =>
        _issueStore.ListAsync(_ruleFlowKey, cancellationToken);

    public ValueTask<NoIssueConclusion?> GetNoIssueConclusionAsync(CancellationToken cancellationToken)
        =>
        _issueStore.GetNoIssueConclusionAsync(_ruleFlowKey, cancellationToken);

    public ValueTask<StoredRuleReviewIssue> UpdateRuleReviewIssueAsync(
        UpdateRuleReviewIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReviewIssueId);
        return _issueStore.UpdateAsync(_ruleFlowKey, args.RuleReviewIssueId.Trim(), CreateIssue(args), cancellationToken);
    }

    public ValueTask<bool> DeleteRuleReviewIssueAsync(
        DeleteRuleReviewIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReviewIssueId);
        return _issueStore.DeleteAsync(_ruleFlowKey, args.RuleReviewIssueId.Trim(), cancellationToken);
    }

    public async ValueTask<bool> SubmitNoIssueConclusionAsync(
        SubmitNoIssueConclusionArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        await _issueStore.SubmitNoIssueConclusionAsync(
            _ruleFlowKey,
            new NoIssueConclusion
            {
                ReviewStrategy = args.ReviewStrategy,
                ScopeCoverage = args.ScopeCoverage,
                CrossScopeAnalysis = args.CrossScopeAnalysis,
                WhyNoIssueWasFound = args.WhyNoIssueWasFound,
            },
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static RuleReviewIssue CreateIssue(CreateRuleReviewIssueArgs args) =>
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

    private static RuleReviewIssue CreateIssue(UpdateRuleReviewIssueArgs args) =>
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
