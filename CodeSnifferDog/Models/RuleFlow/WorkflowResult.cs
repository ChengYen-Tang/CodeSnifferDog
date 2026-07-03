using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using RuleReviewWorkflowResult = CodeSnifferDog.Models.RuleReview.WorkflowResult;

namespace CodeSnifferDog.Models.RuleFlow;

public sealed class WorkflowResult
{
    public required RuleReviewWorkflowResult ReviewResult { get; init; }

    public ReportWorkflowResult? ReportResult { get; init; }

    public required CompletionState CompletionState { get; init; }

    public StoredTaskItem TaskItem => ReviewResult.TaskItem;

    public string RuleKey => ReviewResult.RuleKey;

    public bool IsApprovedCompletion =>
        CompletionState is CompletionState.ApprovedNoIssue or CompletionState.ApprovedWithReport;

    public bool IsDegradedCompletion => !IsApprovedCompletion;
}
