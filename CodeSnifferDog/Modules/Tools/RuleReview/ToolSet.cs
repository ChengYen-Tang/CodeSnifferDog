using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.RuleReview.Tools;
using CodeSnifferDog.Models.RuleReview.Tools.Listing;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

/// <summary>
/// Builds the tool set used by rule-review agents and verifiers.
/// </summary>
public sealed class ToolSet
{
    private readonly IssueToolService _issueToolService;
    private readonly ReviewVerdictToolService _verdictToolService;
    private readonly string _reviewVerdictScopeKey;

    public ToolSet(
        IIssueStore issueStore,
        ReviewVerdictBuffer verdictBuffer,
        RuleFlowKey ruleFlowKey)
        : this(
            new IssueToolService(issueStore, ruleFlowKey),
            new ReviewVerdictToolService(verdictBuffer),
            RuleScopeKeyFactory.CreateReviewVerdictScopeKey(ruleFlowKey))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolSet"/> class for tests or composed services.
    /// </summary>
    /// <param name="issueToolService">Service that manages stored rule-review issues.</param>
    /// <param name="verdictToolService">Service that stores verifier verdicts.</param>
    /// <param name="reviewVerdictScopeKey">Verdict scope key for the current rule flow.</param>
    internal ToolSet(
        IssueToolService issueToolService,
        ReviewVerdictToolService verdictToolService,
        string reviewVerdictScopeKey)
    {
        _issueToolService = issueToolService;
        _verdictToolService = verdictToolService;
        _reviewVerdictScopeKey = reviewVerdictScopeKey;
    }

    /// <summary>
    /// Creates the tools used by rule-review agents.
    /// </summary>
    /// <returns>The rule-review agent tools.</returns>
    public IList<AITool> CreateRuleReviewAgentTools()
        =>
        ToolFactory.CreateAgentTools(new AgentToolCallbacks(
            CreateRuleReviewIssueToolAsync,
            GetRuleReviewIssueToolAsync,
            ListRuleReviewIssuesToolAsync,
            UpdateRuleReviewIssueToolAsync,
            DeleteRuleReviewIssueToolAsync,
            SubmitNoIssueConclusionToolAsync));

    /// <summary>
    /// Creates the tools used by rule-review verifiers.
    /// </summary>
    /// <returns>The rule-review verifier tools.</returns>
    public IList<AITool> CreateVerifierTools()
        =>
        ToolFactory.CreateVerifierTools(new VerifierToolCallbacks(SubmitReviewVerdictToolAsync));

    [Description("Create one new review issue for the current rule review attempt.")]
    private ValueTask<CreateRuleReviewIssueResult> CreateRuleReviewIssueToolAsync(
        [Description("The issue type for the discovered problem.")]
        string IssueType,
        [Description("The severity level for the discovered problem. Allowed values: High, Medium, Low.")]
        string Severity,
        [Description("The related file or function for the discovered problem.")]
        string FileOrFunction,
        [Description("The relevant code pattern or expression that supports the issue.")]
        string RelevantCodePatternOrExpression,
        [Description("Why this is a problem under the current rule.")]
        string WhyThisIsAProblem,
        [Description("The confidence level for this issue, typically High, Medium, or Low.")]
        string Confidence,
        [Description("Any follow-up files that should be referenced for this issue.")]
        string FollowUpFiles,
        [Description("The suggested fix direction for this issue.")]
        string SuggestedFixDirection,
        [Description("What scope entry files were inspected, what was skipped, why, and whether coverage is sufficient.")]
        string ScopeCoverage,
        [Description("What cross-scope analysis was performed, which follow-up files were inspected, and why.")]
        string CrossScopeAnalysis,
        [Description("The review strategy used to discover and validate this issue.")]
        string ReviewStrategy,
        CancellationToken cancellationToken) =>
        CreateRuleReviewIssueAsync(
            new CreateRuleReviewIssueArgs
            {
                IssueType = IssueType,
                Severity = Severity,
                FileOrFunction = FileOrFunction,
                RelevantCodePatternOrExpression = RelevantCodePatternOrExpression,
                WhyThisIsAProblem = WhyThisIsAProblem,
                Confidence = Confidence,
                FollowUpFiles = FollowUpFiles,
                SuggestedFixDirection = SuggestedFixDirection,
                ScopeCoverage = ScopeCoverage,
                CrossScopeAnalysis = CrossScopeAnalysis,
                ReviewStrategy = ReviewStrategy,
            },
            cancellationToken);

    [Description("Get one stored review issue by its id from the current rule review attempt.")]
    private ValueTask<StoredIssue> GetRuleReviewIssueToolAsync(
        [Description("The id of the stored review issue to retrieve.")]
        string RuleReviewIssueId,
        CancellationToken cancellationToken) =>
        GetRuleReviewIssueAsync(
            new GetRuleReviewIssueArgs
            {
                RuleReviewIssueId = RuleReviewIssueId,
            },
            cancellationToken);

    [Description("List one bounded page of review issue indexes. Use GetRuleReviewIssue for the complete issue details.")]
    private ValueTask<IssuePage> ListRuleReviewIssuesToolAsync(
        [Description("The continuation cursor returned by the preceding page. Omit it to start from the first page.")]
        string? Cursor = null,
        [Description("The number of issue indexes to return. Defaults to 10 and cannot exceed 20.")]
        int PageSize = IssuePage.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ListRuleReviewIssuesAsync(
            new ListIssuesArgs
            {
                Cursor = Cursor,
                PageSize = PageSize,
            },
            cancellationToken);

    [Description("Update one existing review issue by its id for the current rule review attempt.")]
    private ValueTask<StoredIssue> UpdateRuleReviewIssueToolAsync(
        [Description("The id of the stored review issue to update.")]
        string RuleReviewIssueId,
        [Description("The updated issue type.")]
        string IssueType,
        [Description("The updated severity level. Allowed values: High, Medium, Low.")]
        string Severity,
        [Description("The updated related file or function.")]
        string FileOrFunction,
        [Description("The updated relevant code pattern or expression.")]
        string RelevantCodePatternOrExpression,
        [Description("The updated explanation of why this is a problem.")]
        string WhyThisIsAProblem,
        [Description("The updated confidence level.")]
        string Confidence,
        [Description("The updated follow-up files.")]
        string FollowUpFiles,
        [Description("The updated suggested fix direction.")]
        string SuggestedFixDirection,
        [Description("The updated scope coverage explanation.")]
        string ScopeCoverage,
        [Description("The updated cross-scope analysis explanation.")]
        string CrossScopeAnalysis,
        [Description("The updated review strategy.")]
        string ReviewStrategy,
        CancellationToken cancellationToken) =>
        UpdateRuleReviewIssueAsync(
            new UpdateRuleReviewIssueArgs
            {
                RuleReviewIssueId = RuleReviewIssueId,
                IssueType = IssueType,
                Severity = Severity,
                FileOrFunction = FileOrFunction,
                RelevantCodePatternOrExpression = RelevantCodePatternOrExpression,
                WhyThisIsAProblem = WhyThisIsAProblem,
                Confidence = Confidence,
                FollowUpFiles = FollowUpFiles,
                SuggestedFixDirection = SuggestedFixDirection,
                ScopeCoverage = ScopeCoverage,
                CrossScopeAnalysis = CrossScopeAnalysis,
                ReviewStrategy = ReviewStrategy,
            },
            cancellationToken);

    [Description("Delete one existing review issue by its id from the current rule review attempt.")]
    private ValueTask<bool> DeleteRuleReviewIssueToolAsync(
        [Description("The id of the stored review issue to delete.")]
        string RuleReviewIssueId,
        CancellationToken cancellationToken) =>
        DeleteRuleReviewIssueAsync(
            new DeleteRuleReviewIssueArgs
            {
                RuleReviewIssueId = RuleReviewIssueId,
            },
            cancellationToken);

    [Description("Submit a no-issue conclusion for the current rule review attempt when no issues exist.")]
    private ValueTask<bool> SubmitNoIssueConclusionToolAsync(
        [Description("The review strategy used before concluding that no issue exists.")]
        string ReviewStrategy,
        [Description("What scope entry files were inspected, what was skipped, why, and whether coverage is sufficient.")]
        string ScopeCoverage,
        [Description("What cross-scope analysis was performed, which follow-up files were inspected, and why.")]
        string CrossScopeAnalysis,
        [Description("Why no issue was found under the current rule.")]
        string WhyNoIssueWasFound,
        CancellationToken cancellationToken) =>
        SubmitNoIssueConclusionAsync(
            new SubmitNoIssueConclusionArgs
            {
                ReviewStrategy = ReviewStrategy,
                ScopeCoverage = ScopeCoverage,
                CrossScopeAnalysis = CrossScopeAnalysis,
                WhyNoIssueWasFound = WhyNoIssueWasFound,
            },
            cancellationToken);

    [Description("Submit the verifier approval or rejection for the current rule review result.")]
    private ValueTask<bool> SubmitReviewVerdictToolAsync(
        [Description("True when the current rule review result is approved. False when more work is required.")]
        bool Approved,
        [Description("The approval note or the rejection reason that explains what the reviewer should keep or fix.")]
        string Message,
        CancellationToken cancellationToken) =>
        SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = Approved,
                Message = Message,
            },
            cancellationToken);

    /// <summary>
    /// Creates one stored rule-review issue.
    /// </summary>
    public ValueTask<CreateRuleReviewIssueResult> CreateRuleReviewIssueAsync(
        CreateRuleReviewIssueArgs args,
        CancellationToken cancellationToken) =>
        _issueToolService.CreateRuleReviewIssueAsync(args, cancellationToken);

    /// <summary>
    /// Gets one stored rule-review issue.
    /// </summary>
    public ValueTask<StoredIssue> GetRuleReviewIssueAsync(
        GetRuleReviewIssueArgs args,
        CancellationToken cancellationToken) =>
        _issueToolService.GetRuleReviewIssueAsync(args, cancellationToken);

    /// <summary>
    /// Lists one bounded page of rule-review issue indexes.
    /// </summary>
    public ValueTask<IssuePage> ListRuleReviewIssuesAsync(
        ListIssuesArgs args,
        CancellationToken cancellationToken)
        =>
        _issueToolService.ListRuleReviewIssuesAsync(args, cancellationToken);

    /// <summary>
    /// Gets the submitted no-issue conclusion, if one exists.
    /// </summary>
    public ValueTask<NoIssueConclusion?> GetNoIssueConclusionAsync(CancellationToken cancellationToken)
        =>
        _issueToolService.GetNoIssueConclusionAsync(cancellationToken);

    /// <summary>
    /// Updates one stored rule-review issue.
    /// </summary>
    public ValueTask<StoredIssue> UpdateRuleReviewIssueAsync(
        UpdateRuleReviewIssueArgs args,
        CancellationToken cancellationToken) =>
        _issueToolService.UpdateRuleReviewIssueAsync(args, cancellationToken);

    /// <summary>
    /// Deletes one stored rule-review issue.
    /// </summary>
    public ValueTask<bool> DeleteRuleReviewIssueAsync(
        DeleteRuleReviewIssueArgs args,
        CancellationToken cancellationToken) =>
        _issueToolService.DeleteRuleReviewIssueAsync(args, cancellationToken);

    /// <summary>
    /// Submits a no-issue conclusion for the current rule flow.
    /// </summary>
    public ValueTask<bool> SubmitNoIssueConclusionAsync(
        SubmitNoIssueConclusionArgs args,
        CancellationToken cancellationToken) =>
        _issueToolService.SubmitNoIssueConclusionAsync(args, cancellationToken);

    /// <summary>
    /// Stores the verifier verdict for the current rule flow.
    /// </summary>
    public ValueTask<bool> SubmitReviewVerdictAsync(
        SubmitReviewVerdictArgs args,
        CancellationToken _) =>
        _verdictToolService.SubmitReviewVerdictAsync(_reviewVerdictScopeKey, args);
}
