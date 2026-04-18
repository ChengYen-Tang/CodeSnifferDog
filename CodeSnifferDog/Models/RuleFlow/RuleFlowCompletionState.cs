namespace CodeSnifferDog.Models.RuleFlow;

public enum RuleFlowCompletionState
{
    ApprovedNoIssue,
    ApprovedWithReport,
    DegradedNoIssue,
    DegradedWithReport,
    DegradedMissingSubmission,
}
