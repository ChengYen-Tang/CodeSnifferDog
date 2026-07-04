namespace CodeSnifferDog.Models.RuleReview;

/// <summary>
/// Configures retry and timeout behavior for the rule-review workflow.
/// </summary>
public sealed class WorkflowOptions
{
    /// <summary>
    /// Gets the maximum number of verifier rejections tolerated before the workflow continues or stops.
    /// </summary>
    public int MaxVerifierRejectionAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the maximum number of missing-submission retries allowed for the review agent.
    /// </summary>
    public int MaxMissingSubmissionAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the maximum number of rule-review agent resets allowed.
    /// </summary>
    public int MaxRuleReviewAgentResets { get; init; } = 3;

    /// <summary>
    /// Gets the maximum number of consecutive execution failures allowed before the workflow stops retrying.
    /// </summary>
    public int MaxConsecutiveRunFailures { get; init; } = Scan.AgentExecutionOptionsDefaults.MaxConsecutiveRunFailures;

    /// <summary>
    /// Gets the maximum duration allowed for one agent run.
    /// </summary>
    public TimeSpan AgentRunTimeout { get; init; } = Scan.AgentExecutionOptionsDefaults.AgentRunTimeout;
}
