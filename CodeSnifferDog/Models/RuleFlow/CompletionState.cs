namespace CodeSnifferDog.Models.RuleFlow;

public enum CompletionState
{
    ApprovedNoIssue,
    ApprovedWithReport,
    DegradedNoIssue,
    DegradedWithReport,
    DegradedMissingSubmission,
}
