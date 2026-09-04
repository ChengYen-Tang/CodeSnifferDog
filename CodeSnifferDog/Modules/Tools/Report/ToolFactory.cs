using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Report.Tools;
using CodeSnifferDog.Models.Report.Tools.Listing;

namespace CodeSnifferDog.Modules.Tools.Report;

/// <summary>
/// Creates the AI tools exposed to report aggregators and verifiers.
/// </summary>
internal static class ToolFactory
{
    /// <summary>
    /// Creates the tools used by report aggregators.
    /// </summary>
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
            "List one bounded page of repository-level rule report issue indexes. Use GetRuleReportIssue for complete issue details.",
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

    /// <summary>
    /// Creates the tools used by report verifiers.
    /// </summary>
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

/// <summary>
/// Groups callbacks used by report-aggregator tools.
/// </summary>
internal readonly record struct AggregatorToolCallbacks(
    GetRuleReportIssueToolCallback GetRuleReportIssueTool,
    ListRuleReportIssuesToolCallback ListRuleReportIssuesTool,
    CreateRuleReportIssueToolCallback CreateRuleReportIssueTool,
    UpdateRuleReportIssueToolCallback UpdateRuleReportIssueTool,
    DeleteRuleReportIssueToolCallback DeleteRuleReportIssueTool);

/// <summary>
/// Groups callbacks used by report-verifier tools.
/// </summary>
internal readonly record struct VerifierToolCallbacks(
    SubmitReviewVerdictToolCallback SubmitReviewVerdictTool);

/// <summary>
/// Represents the callback used to retrieve one stored report issue.
/// </summary>
internal delegate ValueTask<StoredIssue> GetRuleReportIssueToolCallback(
    string RuleReportIssueId,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to list stored report issues.
/// </summary>
internal delegate ValueTask<IssuePage> ListRuleReportIssuesToolCallback(
    string? Cursor = null,
    int PageSize = IssuePage.DefaultPageSize,
    CancellationToken cancellationToken = default);

/// <summary>
/// Represents the callback used to create one report issue.
/// </summary>
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

/// <summary>
/// Represents the callback used to update one report issue.
/// </summary>
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

/// <summary>
/// Represents the callback used to delete one report issue.
/// </summary>
internal delegate ValueTask<bool> DeleteRuleReportIssueToolCallback(
    string RuleReportIssueId,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to submit the verifier verdict.
/// </summary>
internal delegate ValueTask<bool> SubmitReviewVerdictToolCallback(
    bool Approved,
    string Message,
    CancellationToken cancellationToken);
