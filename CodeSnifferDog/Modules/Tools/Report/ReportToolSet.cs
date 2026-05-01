using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace CodeSnifferDog.Modules.Tools.Report;

public sealed class ReportToolSet(
    IRuleReportIssueStore reportIssueStore,
    ReviewVerdictBuffer verdictBuffer,
    RuleFlowKey ruleFlowKey,
    RuleReportKey ruleReportKey)
{
    private readonly IRuleReportIssueStore _reportIssueStore = reportIssueStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly RuleFlowKey _ruleFlowKey = ruleFlowKey;
    private readonly RuleReportKey _ruleReportKey = ruleReportKey;
    private readonly string _reportVerdictScopeKey = RuleScopeKeyFactory.CreateReportVerdictScopeKey(ruleFlowKey);

    public IList<AITool> CreateReportAggregatorTools()
        =>
    [
        AIFunctionFactory.Create(
            GetRuleReportIssueToolAsync,
            "GetRuleReportIssue",
            "Get one stored repository-level rule report issue by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            ListRuleReportIssuesAsync,
            "ListRuleReportIssues",
            "List all repository-level rule report issues for the current rule.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            CreateRuleReportIssueToolAsync,
            "CreateRuleReportIssue",
            "Create one new repository-level rule report issue for the current rule.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            UpdateRuleReportIssueToolAsync,
            "UpdateRuleReportIssue",
            "Update one existing repository-level rule report issue by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            DeleteRuleReportIssueToolAsync,
            "DeleteRuleReportIssue",
            "Delete one existing repository-level rule report issue by its id.",
            serializerOptions: null),
    ];

    public IList<AITool> CreateVerifierTools()
        =>
    [
        AIFunctionFactory.Create(
            SubmitReviewVerdictToolAsync,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current rule report diff.",
            serializerOptions: null),
    ];

    [Description("Get one stored repository-level rule report issue by its id.")]
    private ValueTask<StoredRuleReportIssue> GetRuleReportIssueToolAsync(
        [Description("The id of the stored repository-level rule report issue to retrieve.")]
        string RuleReportIssueId,
        CancellationToken cancellationToken) =>
        GetRuleReportIssueAsync(
            new GetRuleReportIssueArgs
            {
                RuleReportIssueId = RuleReportIssueId,
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
    private ValueTask<StoredRuleReportIssue> UpdateRuleReportIssueToolAsync(
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

    public ValueTask<bool> SubmitReviewVerdictAsync(
        SubmitReviewVerdictArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Message);
        _verdictBuffer.Submit(_reportVerdictScopeKey, args.Approved, args.Message.Trim());
        return ValueTask.FromResult(true);
    }

    private static RuleReviewIssue CreateIssue(CreateRuleReportIssueArgs args)
    {
        ValidateIssueFields(
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

        return new RuleReviewIssue
        {
            IssueType = args.IssueType.Trim(),
            Severity = RuleReviewSeverity.Normalize(args.Severity),
            FileOrFunction = args.FileOrFunction.Trim(),
            RelevantCodePatternOrExpression = args.RelevantCodePatternOrExpression.Trim(),
            WhyThisIsAProblem = args.WhyThisIsAProblem.Trim(),
            Confidence = args.Confidence.Trim(),
            FollowUpFiles = args.FollowUpFiles.Trim(),
            SuggestedFixDirection = args.SuggestedFixDirection.Trim(),
            ScopeCoverage = args.ScopeCoverage.Trim(),
            CrossScopeAnalysis = args.CrossScopeAnalysis.Trim(),
            ReviewStrategy = args.ReviewStrategy.Trim(),
        };
    }

    private static RuleReviewIssue CreateIssue(UpdateRuleReportIssueArgs args)
    {
        ValidateIssueFields(
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

        return new RuleReviewIssue
        {
            IssueType = args.IssueType.Trim(),
            Severity = RuleReviewSeverity.Normalize(args.Severity),
            FileOrFunction = args.FileOrFunction.Trim(),
            RelevantCodePatternOrExpression = args.RelevantCodePatternOrExpression.Trim(),
            WhyThisIsAProblem = args.WhyThisIsAProblem.Trim(),
            Confidence = args.Confidence.Trim(),
            FollowUpFiles = args.FollowUpFiles.Trim(),
            SuggestedFixDirection = args.SuggestedFixDirection.Trim(),
            ScopeCoverage = args.ScopeCoverage.Trim(),
            CrossScopeAnalysis = args.CrossScopeAnalysis.Trim(),
            ReviewStrategy = args.ReviewStrategy.Trim(),
        };
    }

    private static void ValidateIssueFields(
        string issueType,
        string severity,
        string fileOrFunction,
        string relevantCodePatternOrExpression,
        string whyThisIsAProblem,
        string confidence,
        string followUpFiles,
        string suggestedFixDirection,
        string scopeCoverage,
        string crossScopeAnalysis,
        string reviewStrategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueType);
        RuleReviewSeverity.Normalize(severity);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileOrFunction);
        ArgumentException.ThrowIfNullOrWhiteSpace(relevantCodePatternOrExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(whyThisIsAProblem);
        ArgumentException.ThrowIfNullOrWhiteSpace(confidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(followUpFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFixDirection);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeCoverage);
        ArgumentException.ThrowIfNullOrWhiteSpace(crossScopeAnalysis);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewStrategy);
    }
}
