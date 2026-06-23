using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.RuleReview.Tools;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

internal static class RuleReviewToolFactory
{
    public static IList<AITool> CreateAgentTools(RuleReviewAgentToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.CreateRuleReviewIssueTool,
            "CreateRuleReviewIssue",
            "Create one new review issue for the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.GetRuleReviewIssueTool,
            "GetRuleReviewIssue",
            "Get one stored review issue by its id from the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.ListRuleReviewIssuesTool,
            "ListRuleReviewIssues",
            "List all stored review issues for the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.UpdateRuleReviewIssueTool,
            "UpdateRuleReviewIssue",
            "Update one existing review issue by its id for the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.DeleteRuleReviewIssueTool,
            "DeleteRuleReviewIssue",
            "Delete one existing review issue by its id from the current rule review attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.SubmitNoIssueConclusionTool,
            "SubmitNoIssueConclusion",
            "Submit a no-issue conclusion for the current rule review attempt when no issues exist.",
            serializerOptions: null),
    ];

    public static IList<AITool> CreateVerifierTools(RuleReviewVerifierToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.SubmitReviewVerdictTool,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current rule review result.",
            serializerOptions: null),
    ];
}

internal readonly record struct RuleReviewAgentToolCallbacks(
    CreateRuleReviewIssueToolCallback CreateRuleReviewIssueTool,
    GetRuleReviewIssueToolCallback GetRuleReviewIssueTool,
    ListRuleReviewIssuesToolCallback ListRuleReviewIssuesTool,
    UpdateRuleReviewIssueToolCallback UpdateRuleReviewIssueTool,
    DeleteRuleReviewIssueToolCallback DeleteRuleReviewIssueTool,
    SubmitNoIssueConclusionToolCallback SubmitNoIssueConclusionTool);

internal readonly record struct RuleReviewVerifierToolCallbacks(
    SubmitReviewVerdictToolCallback SubmitReviewVerdictTool);

internal delegate ValueTask<CreateRuleReviewIssueResult> CreateRuleReviewIssueToolCallback(
    string IssueType,
    string Severity,
    string FileOrFunction,
    string RelevantCodePatternOrExpression,
    string WhyThisIsAProblem,
    string Confidence,
    string FollowUpFiles,
    string SuggestedFixDirection,
    string ScopeCoverage,
    string CrossScopeAnalysis,
    string ReviewStrategy,
    CancellationToken cancellationToken);

internal delegate ValueTask<StoredRuleReviewIssue> GetRuleReviewIssueToolCallback(
    string RuleReviewIssueId,
    CancellationToken cancellationToken);

internal delegate ValueTask<IReadOnlyList<StoredRuleReviewIssue>> ListRuleReviewIssuesToolCallback(
    CancellationToken cancellationToken);

internal delegate ValueTask<StoredRuleReviewIssue> UpdateRuleReviewIssueToolCallback(
    string RuleReviewIssueId,
    string IssueType,
    string Severity,
    string FileOrFunction,
    string RelevantCodePatternOrExpression,
    string WhyThisIsAProblem,
    string Confidence,
    string FollowUpFiles,
    string SuggestedFixDirection,
    string ScopeCoverage,
    string CrossScopeAnalysis,
    string ReviewStrategy,
    CancellationToken cancellationToken);

internal delegate ValueTask<bool> DeleteRuleReviewIssueToolCallback(
    string RuleReviewIssueId,
    CancellationToken cancellationToken);

internal delegate ValueTask<bool> SubmitNoIssueConclusionToolCallback(
    string ReviewStrategy,
    string ScopeCoverage,
    string CrossScopeAnalysis,
    string WhyNoIssueWasFound,
    CancellationToken cancellationToken);

internal delegate ValueTask<bool> SubmitReviewVerdictToolCallback(
    bool Approved,
    string Message,
    CancellationToken cancellationToken);
