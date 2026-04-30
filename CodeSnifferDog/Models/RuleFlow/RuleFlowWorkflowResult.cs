using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Models.RuleFlow;

public sealed class RuleFlowWorkflowResult
{
    public required StoredProjectPlanTaskItem TaskItem { get; init; }

    public required string RuleKey { get; init; }

    public required RuleReviewWorkflowResult ReviewResult { get; init; }

    public RuleReportWorkflowResult? ReportResult { get; init; }

    public required bool EnteredReportAggregation { get; init; }

    public required RuleFlowCompletionState CompletionState { get; init; }

    public bool IsApprovedCompletion =>
        CompletionState is RuleFlowCompletionState.ApprovedNoIssue or RuleFlowCompletionState.ApprovedWithReport;

    public bool IsDegradedCompletion => !IsApprovedCompletion;
}
