using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;
using CodeSnifferDog.Models.Report.Tools.Listing;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;

namespace CodeSnifferDog.Modules.Tools.Report;

/// <summary>
/// Builds the tool set used by report aggregators and verifiers.
/// </summary>
public sealed class ToolSet
{
    private readonly IssueToolService _issueToolService;
    private readonly ReviewVerdictToolService _verdictToolService;
    private readonly string _reportVerdictScopeKey;

    public ToolSet(
        IIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer,
        RuleFlowKey ruleFlowKey,
        RuleReportKey ruleReportKey)
        : this(
            new IssueToolService(reportIssueStore, ruleFlowKey),
            new ReviewVerdictToolService(verdictBuffer),
            RuleScopeKeyFactory.CreateReportVerdictScopeKey(ruleFlowKey))
    {
        // Kept for workflow constructor compatibility; working report state remains scoped by RuleFlowKey.
        _ = ruleReportKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolSet"/> class for tests or composed services.
    /// </summary>
    internal ToolSet(
        IssueToolService issueToolService,
        ReviewVerdictToolService verdictToolService,
        string reportVerdictScopeKey)
    {
        _issueToolService = issueToolService;
        _verdictToolService = verdictToolService;
        _reportVerdictScopeKey = reportVerdictScopeKey;
    }

    /// <summary>
    /// Creates the tools used by report aggregators.
    /// </summary>
    public IList<AITool> CreateReportAggregatorTools()
        =>
        ToolFactory.CreateAggregatorTools(new AggregatorToolCallbacks(
            GetRuleReportIssueToolAsync,
            ListRuleReportIssuesToolAsync,
            CreateRuleReportIssueToolAsync,
            UpdateRuleReportIssueToolAsync,
            DeleteRuleReportIssueToolAsync));

    /// <summary>
    /// Creates the tools used by report verifiers.
    /// </summary>
    public IList<AITool> CreateVerifierTools()
        =>
        ToolFactory.CreateVerifierTools(new VerifierToolCallbacks(SubmitReviewVerdictToolAsync));

    [Description("Get one stored repository-level rule report issue by its id.")]
    private ValueTask<ReportStoredIssue> GetRuleReportIssueToolAsync(
        [Description("The id of the stored repository-level rule report issue to retrieve.")]
        string RuleReportIssueId,
        CancellationToken cancellationToken) =>
        GetRuleReportIssueAsync(
            new GetRuleReportIssueArgs
            {
                RuleReportIssueId = RuleReportIssueId,
            },
            cancellationToken);

    [Description("List one bounded page of repository-level rule report issue indexes. Use GetRuleReportIssue for complete issue details.")]
    private ValueTask<IssuePage> ListRuleReportIssuesToolAsync(
        [Description("The continuation cursor returned by the preceding page. Omit it to start from the first page.")]
        string? Cursor = null,
        [Description("The number of issue indexes to return. Defaults to 10 and cannot exceed 20.")]
        int PageSize = IssuePage.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ListRuleReportIssuesAsync(
            new ListIssuesArgs
            {
                Cursor = Cursor,
                PageSize = PageSize,
            },
            cancellationToken);

    [Description("Create one new repository-level rule report issue for the current rule.")]
    private ValueTask<CreateRuleReportIssueResult> CreateRuleReportIssueToolAsync(
        [Description("The issue type for the repository-level issue.")]
        string IssueType,
        [Description("The severity level for the repository-level issue. Allowed values: High, Medium, Low.")]
        string Severity,
        [Description("The related file or function for the repository-level issue.")]
        string FileOrFunction,
        [Description("The relevant code pattern or expression for the repository-level issue.")]
        string RelevantCodePatternOrExpression,
        [Description("Why this is a problem for the repository-level issue.")]
        string WhyThisIsAProblem,
        [Description("The confidence level for this repository-level issue.")]
        string Confidence,
        [Description("Any follow-up files that support this repository-level issue.")]
        string FollowUpFiles,
        [Description("The suggested fix direction for this repository-level issue.")]
        string SuggestedFixDirection,
        [Description("The scope coverage explanation for this repository-level issue.")]
        string ScopeCoverage,
        [Description("The cross-scope analysis explanation for this repository-level issue.")]
        string CrossScopeAnalysis,
        [Description("The review strategy for this repository-level issue.")]
        string ReviewStrategy,
        CancellationToken cancellationToken) =>
        CreateRuleReportIssueAsync(
            new CreateRuleReportIssueArgs
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

    [Description("Update one existing repository-level rule report issue by its id.")]
    private ValueTask<ReportStoredIssue> UpdateRuleReportIssueToolAsync(
        [Description("The id of the stored repository-level rule report issue to update.")]
        string RuleReportIssueId,
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
        UpdateRuleReportIssueAsync(
            new UpdateRuleReportIssueArgs
            {
                RuleReportIssueId = RuleReportIssueId,
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

    [Description("Delete one existing repository-level rule report issue by its id.")]
    private ValueTask<bool> DeleteRuleReportIssueToolAsync(
        [Description("The id of the stored repository-level rule report issue to delete.")]
        string RuleReportIssueId,
        CancellationToken cancellationToken) =>
        DeleteRuleReportIssueAsync(
            new DeleteRuleReportIssueArgs
            {
                RuleReportIssueId = RuleReportIssueId,
            },
            cancellationToken);

    [Description("Submit the verifier approval or rejection for the current rule report diff.")]
    private ValueTask<bool> SubmitReviewVerdictToolAsync(
        [Description("True when the current rule report diff is approved. False when more work is required.")]
        bool Approved,
        [Description("The approval note or the rejection reason that explains what the aggregator should keep or fix.")]
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
    /// Gets one stored repository-level issue.
    /// </summary>
    public ValueTask<ReportStoredIssue> GetRuleReportIssueAsync(
        GetRuleReportIssueArgs args,
        CancellationToken cancellationToken) =>
        _issueToolService.GetRuleReportIssueAsync(args, cancellationToken);

    /// <summary>
    /// Lists one bounded page of repository-level issue indexes.
    /// </summary>
    public ValueTask<IssuePage> ListRuleReportIssuesAsync(
        ListIssuesArgs args,
        CancellationToken cancellationToken)
        =>
        _issueToolService.ListRuleReportIssuesAsync(args, cancellationToken);

    /// <summary>
    /// Creates one stored repository-level issue.
    /// </summary>
    public ValueTask<CreateRuleReportIssueResult> CreateRuleReportIssueAsync(
        CreateRuleReportIssueArgs args,
        CancellationToken cancellationToken) =>
        _issueToolService.CreateRuleReportIssueAsync(args, cancellationToken);

    /// <summary>
    /// Updates one stored repository-level issue.
    /// </summary>
    public ValueTask<ReportStoredIssue> UpdateRuleReportIssueAsync(
        UpdateRuleReportIssueArgs args,
        CancellationToken cancellationToken) =>
        _issueToolService.UpdateRuleReportIssueAsync(args, cancellationToken);

    /// <summary>
    /// Deletes one stored repository-level issue.
    /// </summary>
    public ValueTask<bool> DeleteRuleReportIssueAsync(
        DeleteRuleReportIssueArgs args,
        CancellationToken cancellationToken) =>
        _issueToolService.DeleteRuleReportIssueAsync(args, cancellationToken);

    /// <summary>
    /// Gets the latest diff for the working report.
    /// </summary>
    public ValueTask<Diff> GetLatestDiffAsync(CancellationToken cancellationToken)
        =>
        _issueToolService.GetLatestDiffAsync(cancellationToken);

    /// <summary>
    /// Stores the latest diff for the working report.
    /// </summary>
    public ValueTask SetLatestDiffAsync(Diff diff, CancellationToken cancellationToken)
        =>
        _issueToolService.SetLatestDiffAsync(diff, cancellationToken);

    /// <summary>
    /// Stores the verifier verdict for the current report flow.
    /// </summary>
    public ValueTask<bool> SubmitReviewVerdictAsync(
        SubmitReviewVerdictArgs args,
        CancellationToken _) =>
        _verdictToolService.SubmitReviewVerdictAsync(_reportVerdictScopeKey, args);
}
