using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Models.RuleFlow;

public sealed class RuleFlowWorkflowResult
{
    public required RuleReviewWorkflowResult ReviewResult { get; init; }

    public RuleReportWorkflowResult? ReportResult { get; init; }

    public required RuleFlowCompletionState CompletionState { get; init; }

    public StoredProjectPlanTaskItem TaskItem => ReviewResult.TaskItem;

    public string RuleKey => ReviewResult.RuleKey;

    public bool IsApprovedCompletion =>
        CompletionState is RuleFlowCompletionState.ApprovedNoIssue or RuleFlowCompletionState.ApprovedWithReport;

    public bool IsDegradedCompletion => !IsApprovedCompletion;
}
