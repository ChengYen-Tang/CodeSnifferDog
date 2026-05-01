using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.RuleReview.Tools;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

public sealed class RuleReviewToolSet(
    IRuleReviewIssueStore issueStore,
    ReviewVerdictBuffer verdictBuffer,
    RuleFlowKey ruleFlowKey)
{
    private readonly IRuleReviewIssueStore _issueStore = issueStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly RuleFlowKey _ruleFlowKey = ruleFlowKey;
    private readonly string _reviewVerdictScopeKey = RuleScopeKeyFactory.CreateReviewVerdictScopeKey(ruleFlowKey);

    public IList<AITool> CreateRuleReviewAgentTools()
        =>
    [
        AIFunctionFactory.Create(
            CreateRuleReviewIssueToolAsync,
            "CreateRuleReviewIssue",
            "Create one new review issue for the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            GetRuleReviewIssueToolAsync,
            "GetRuleReviewIssue",
            "Get one stored review issue by its id from the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            ListRuleReviewIssuesAsync,
            "ListRuleReviewIssues",
            "List all stored review issues for the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            UpdateRuleReviewIssueToolAsync,
            "UpdateRuleReviewIssue",
            "Update one existing review issue by its id for the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            DeleteRuleReviewIssueToolAsync,
            "DeleteRuleReviewIssue",
            "Delete one existing review issue by its id from the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            SubmitNoIssueConclusionToolAsync,
            "SubmitNoIssueConclusion",
            "Submit a no-issue conclusion for the current rule review attempt when no issues exist.",
            serializerOptions: null),
    ];

    public IList<AITool> CreateVerifierTools()
        =>
    [
        AIFunctionFactory.Create(
            SubmitReviewVerdictToolAsync,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current rule review result.",
            serializerOptions: null),
    ];

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
    private ValueTask<StoredRuleReviewIssue> GetRuleReviewIssueToolAsync(
        [Description("The id of the stored review issue to retrieve.")]
        string RuleReviewIssueId,
        CancellationToken cancellationToken) =>
        GetRuleReviewIssueAsync(
            new GetRuleReviewIssueArgs
            {
                RuleReviewIssueId = RuleReviewIssueId,
            },
            cancellationToken);

    [Description("Update one existing review issue by its id for the current rule review attempt.")]
    private ValueTask<StoredRuleReviewIssue> UpdateRuleReviewIssueToolAsync(
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

    public ValueTask<bool> SubmitReviewVerdictAsync(
        SubmitReviewVerdictArgs args,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Message);
        _verdictBuffer.Submit(_reviewVerdictScopeKey, args.Approved, args.Message.Trim());
        return ValueTask.FromResult(true);
    }

    private static RuleReviewIssue CreateIssue(CreateRuleReviewIssueArgs args)
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

    private static RuleReviewIssue CreateIssue(UpdateRuleReviewIssueArgs args)
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
