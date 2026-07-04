namespace CodeSnifferDog.Models.Scan;

/// <summary>
/// Configures retry and timeout behavior for the scan workflow.
/// </summary>
public sealed class ScanWorkflowOptions
{
    /// <summary>
    /// Gets the maximum number of verifier rejections tolerated before the workflow continues or stops.
    /// </summary>
    public int MaxVerifierRejectionAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the maximum number of missing-submission retries allowed for the scan agent.
    /// </summary>
    public int MaxMissingSubmissionAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the maximum number of scan-agent resets allowed.
    /// </summary>
    public int MaxScanAgentResets { get; init; } = 3;

    /// <summary>
    /// Gets the maximum number of consecutive execution failures allowed before the workflow stops retrying.
    /// </summary>
    public int MaxConsecutiveRunFailures { get; init; } = AgentExecutionOptionsDefaults.MaxConsecutiveRunFailures;

    /// <summary>
    /// Gets the maximum duration allowed for one agent run.
    /// </summary>
    public TimeSpan AgentRunTimeout { get; init; } = AgentExecutionOptionsDefaults.AgentRunTimeout;
}
