namespace CodeSnifferDog.Models.RuleFlow;

/// <summary>
/// Describes how a rule-flow run completed after review and optional reporting.
/// </summary>
public enum CompletionState
{
    /// <summary>
    /// Review approved the rule flow and no report was needed.
    /// </summary>
    ApprovedNoIssue,

    /// <summary>
    /// Review approved the rule flow after a report was produced.
    /// </summary>
    ApprovedWithReport,

    /// <summary>
    /// Review degraded but ultimately concluded with no issue.
    /// </summary>
    DegradedNoIssue,

    /// <summary>
    /// Review degraded and required a report.
    /// </summary>
    DegradedWithReport,

    /// <summary>
    /// Review degraded because the agent repeatedly failed to submit a result.
    /// </summary>
    DegradedMissingSubmission,
}
