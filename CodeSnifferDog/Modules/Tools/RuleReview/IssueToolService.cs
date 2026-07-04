using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.RuleReview.Tools;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

/// <summary>
/// Validates rule-review tool arguments and delegates issue operations to <see cref="IIssueStore" />.
/// </summary>
internal sealed class IssueToolService(
    IIssueStore issueStore,
    RuleFlowKey ruleFlowKey)
{
    private readonly IIssueStore _issueStore = issueStore;
    private readonly RuleFlowKey _ruleFlowKey = ruleFlowKey;

    /// <summary>
    /// Creates one stored rule-review issue.
    /// </summary>
    public async ValueTask<CreateRuleReviewIssueResult> CreateRuleReviewIssueAsync(
        CreateRuleReviewIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        StoredIssue issue = await _issueStore.AddAsync(
            _ruleFlowKey,
            CreateIssue(args),
            cancellationToken).ConfigureAwait(false);

        return new CreateRuleReviewIssueResult
        {
            RuleReviewIssueId = issue.RuleReviewIssueId,
        };
    }

    /// <summary>
    /// Gets one stored rule-review issue.
    /// </summary>
    public ValueTask<StoredIssue> GetRuleReviewIssueAsync(
        GetRuleReviewIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReviewIssueId);
        return _issueStore.GetAsync(_ruleFlowKey, args.RuleReviewIssueId.Trim(), cancellationToken);
    }

    /// <summary>
    /// Lists the stored rule-review issues.
    /// </summary>
    public ValueTask<IReadOnlyList<StoredIssue>> ListRuleReviewIssuesAsync(CancellationToken cancellationToken)
        =>
        _issueStore.ListAsync(_ruleFlowKey, cancellationToken);

    /// <summary>
    /// Gets the submitted no-issue conclusion, if one exists.
    /// </summary>
    public ValueTask<NoIssueConclusion?> GetNoIssueConclusionAsync(CancellationToken cancellationToken)
        =>
        _issueStore.GetNoIssueConclusionAsync(_ruleFlowKey, cancellationToken);

    /// <summary>
    /// Updates one stored rule-review issue.
    /// </summary>
    public ValueTask<StoredIssue> UpdateRuleReviewIssueAsync(
        UpdateRuleReviewIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReviewIssueId);
        return _issueStore.UpdateAsync(_ruleFlowKey, args.RuleReviewIssueId.Trim(), CreateIssue(args), cancellationToken);
    }

    /// <summary>
    /// Deletes one stored rule-review issue.
    /// </summary>
    public ValueTask<bool> DeleteRuleReviewIssueAsync(
        DeleteRuleReviewIssueArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.RuleReviewIssueId);
        return _issueStore.DeleteAsync(_ruleFlowKey, args.RuleReviewIssueId.Trim(), cancellationToken);
    }

    /// <summary>
    /// Submits a no-issue conclusion for the current rule flow.
    /// </summary>
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

    /// <summary>
    /// Creates a normalized issue from create arguments.
    /// </summary>
    private static Issue CreateIssue(CreateRuleReviewIssueArgs args) =>
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
    private static Issue CreateIssue(UpdateRuleReviewIssueArgs args) =>
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
