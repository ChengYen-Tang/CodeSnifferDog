using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.RuleReview.Tools;

namespace CodeSnifferDog.Modules.Tools.RuleReview;

/// <summary>
/// Creates the AI tools exposed to rule-review agents and verifiers.
/// </summary>
internal static class ToolFactory
{
    /// <summary>
    /// Creates the tools used by rule-review agents.
    /// </summary>
    /// <param name="callbacks">Callbacks invoked by the created tools.</param>
    /// <returns>The rule-review agent tools.</returns>
    public static IList<AITool> CreateAgentTools(AgentToolCallbacks callbacks)
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

    /// <summary>
    /// Creates the tools used by rule-review verifiers.
    /// </summary>
    /// <param name="callbacks">Callbacks invoked by the created tools.</param>
    /// <returns>The rule-review verifier tools.</returns>
    public static IList<AITool> CreateVerifierTools(VerifierToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.SubmitReviewVerdictTool,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current rule review result.",
            serializerOptions: null),
    ];
}

/// <summary>
/// Groups callbacks used by rule-review agent tools.
/// </summary>
/// <param name="CreateRuleReviewIssueTool">Callback for creating one issue.</param>
/// <param name="GetRuleReviewIssueTool">Callback for retrieving one issue.</param>
/// <param name="ListRuleReviewIssuesTool">Callback for listing issues.</param>
/// <param name="UpdateRuleReviewIssueTool">Callback for updating one issue.</param>
/// <param name="DeleteRuleReviewIssueTool">Callback for deleting one issue.</param>
/// <param name="SubmitNoIssueConclusionTool">Callback for submitting a no-issue conclusion.</param>
internal readonly record struct AgentToolCallbacks(
    CreateRuleReviewIssueToolCallback CreateRuleReviewIssueTool,
    GetRuleReviewIssueToolCallback GetRuleReviewIssueTool,
    ListRuleReviewIssuesToolCallback ListRuleReviewIssuesTool,
    UpdateRuleReviewIssueToolCallback UpdateRuleReviewIssueTool,
    DeleteRuleReviewIssueToolCallback DeleteRuleReviewIssueTool,
    SubmitNoIssueConclusionToolCallback SubmitNoIssueConclusionTool);

/// <summary>
/// Groups callbacks used by rule-review verifier tools.
/// </summary>
/// <param name="SubmitReviewVerdictTool">Callback for submitting the verifier verdict.</param>
internal readonly record struct VerifierToolCallbacks(
    SubmitReviewVerdictToolCallback SubmitReviewVerdictTool);

/// <summary>
/// Represents the callback used to create one rule-review issue.
/// </summary>
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

/// <summary>
/// Represents the callback used to retrieve one stored rule-review issue.
/// </summary>
internal delegate ValueTask<StoredIssue> GetRuleReviewIssueToolCallback(
    string RuleReviewIssueId,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to list stored rule-review issues.
/// </summary>
internal delegate ValueTask<IReadOnlyList<StoredIssue>> ListRuleReviewIssuesToolCallback(
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to update one stored rule-review issue.
/// </summary>
internal delegate ValueTask<StoredIssue> UpdateRuleReviewIssueToolCallback(
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

/// <summary>
/// Represents the callback used to delete one stored rule-review issue.
/// </summary>
internal delegate ValueTask<bool> DeleteRuleReviewIssueToolCallback(
    string RuleReviewIssueId,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to submit a no-issue conclusion.
/// </summary>
internal delegate ValueTask<bool> SubmitNoIssueConclusionToolCallback(
    string ReviewStrategy,
    string ScopeCoverage,
    string CrossScopeAnalysis,
    string WhyNoIssueWasFound,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to submit the verifier verdict.
/// </summary>
internal delegate ValueTask<bool> SubmitReviewVerdictToolCallback(
    bool Approved,
    string Message,
    CancellationToken cancellationToken);
