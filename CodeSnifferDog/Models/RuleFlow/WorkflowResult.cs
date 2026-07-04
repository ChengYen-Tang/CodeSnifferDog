using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using RuleReviewWorkflowResult = CodeSnifferDog.Models.RuleReview.WorkflowResult;

namespace CodeSnifferDog.Models.RuleFlow;

/// <summary>
/// Holds the review and optional report outputs produced for one rule flow.
/// </summary>
public sealed class WorkflowResult
{
    /// <summary>
    /// Gets the rule-review result.
    /// </summary>
    public required RuleReviewWorkflowResult ReviewResult { get; init; }

    /// <summary>
    /// Gets the report result when the rule flow escalated to reporting.
    /// </summary>
    public ReportWorkflowResult? ReportResult { get; init; }

    /// <summary>
    /// Gets the final completion state for the rule flow.
    /// </summary>
    public required CompletionState CompletionState { get; init; }

    /// <summary>
    /// Gets the task item associated with the review result.
    /// </summary>
    public StoredTaskItem TaskItem => ReviewResult.TaskItem;

    /// <summary>
    /// Gets the rule key associated with the review result.
    /// </summary>
    public string RuleKey => ReviewResult.RuleKey;

    /// <summary>
    /// Gets whether the rule flow completed in an approved state.
    /// </summary>
    public bool IsApprovedCompletion =>
        CompletionState is CompletionState.ApprovedNoIssue or CompletionState.ApprovedWithReport;

    /// <summary>
    /// Gets whether the rule flow completed in a degraded state.
    /// </summary>
    public bool IsDegradedCompletion => !IsApprovedCompletion;
}
