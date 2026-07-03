using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;

namespace CodeSnifferDog.Modules.Tools.Report;

internal static class ToolFactory
{
    public static IList<AITool> CreateAggregatorTools(AggregatorToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.GetRuleReportIssueTool,
            "GetRuleReportIssue",
            "Get one stored repository-level rule report issue by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.ListRuleReportIssuesTool,
            "ListRuleReportIssues",
            "List all repository-level rule report issues for the current rule.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.CreateRuleReportIssueTool,
            "CreateRuleReportIssue",
            "Create one new repository-level rule report issue for the current rule.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.UpdateRuleReportIssueTool,
            "UpdateRuleReportIssue",
            "Update one existing repository-level rule report issue by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.DeleteRuleReportIssueTool,
            "DeleteRuleReportIssue",
            "Delete one existing repository-level rule report issue by its id.",
            serializerOptions: null),
    ];

    public static IList<AITool> CreateVerifierTools(VerifierToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.SubmitReviewVerdictTool,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current rule report diff.",
            serializerOptions: null),
    ];
}

internal readonly record struct AggregatorToolCallbacks(
    GetRuleReportIssueToolCallback GetRuleReportIssueTool,
    ListRuleReportIssuesToolCallback ListRuleReportIssuesTool,
    CreateRuleReportIssueToolCallback CreateRuleReportIssueTool,
    UpdateRuleReportIssueToolCallback UpdateRuleReportIssueTool,
    DeleteRuleReportIssueToolCallback DeleteRuleReportIssueTool);

internal readonly record struct VerifierToolCallbacks(
    SubmitReviewVerdictToolCallback SubmitReviewVerdictTool);

internal delegate ValueTask<StoredIssue> GetRuleReportIssueToolCallback(
    string RuleReportIssueId,
    CancellationToken cancellationToken);

internal delegate ValueTask<IReadOnlyList<StoredIssue>> ListRuleReportIssuesToolCallback(
    CancellationToken cancellationToken);

internal delegate ValueTask<CreateRuleReportIssueResult> CreateRuleReportIssueToolCallback(
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

internal delegate ValueTask<StoredIssue> UpdateRuleReportIssueToolCallback(
    string RuleReportIssueId,
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

internal delegate ValueTask<bool> DeleteRuleReportIssueToolCallback(
    string RuleReportIssueId,
    CancellationToken cancellationToken);

internal delegate ValueTask<bool> SubmitReviewVerdictToolCallback(
    bool Approved,
    string Message,
    CancellationToken cancellationToken);
