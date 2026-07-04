namespace CodeSnifferDog.Models.ProjectPlan;

/// <summary>
/// Configures retry and timeout behavior for the project-plan workflow.
/// </summary>
public sealed class WorkflowOptions
{
    /// <summary>
    /// Gets the maximum number of verifier rejections tolerated before the workflow continues or stops.
    /// </summary>
    public int MaxVerifierRejectionAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the maximum number of missing-submission retries allowed for the planning agent.
    /// </summary>
    public int MaxMissingSubmissionAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the maximum number of project-plan agent resets allowed.
    /// </summary>
    public int MaxProjectPlanAgentResets { get; init; } = 3;

    /// <summary>
    /// Gets the maximum number of consecutive execution failures allowed before the workflow stops retrying.
    /// </summary>
    public int MaxConsecutiveRunFailures { get; init; } = Scan.AgentExecutionOptionsDefaults.MaxConsecutiveRunFailures;

    /// <summary>
    /// Gets the maximum duration allowed for one agent run.
    /// </summary>
    public TimeSpan AgentRunTimeout { get; init; } = Scan.AgentExecutionOptionsDefaults.AgentRunTimeout;
}
