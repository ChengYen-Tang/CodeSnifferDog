namespace CodeSnifferDog.Models.Report;

public sealed class WorkflowOptions
{
    public int MaxVerifierRejectionAttempts { get; init; } = 3;

    public int MaxMissingSubmissionAttempts { get; init; } = 3;

    public int MaxConsecutiveRunFailures { get; init; } = Scan.AgentExecutionOptionsDefaults.MaxConsecutiveRunFailures;

    public TimeSpan AgentRunTimeout { get; init; } = Scan.AgentExecutionOptionsDefaults.AgentRunTimeout;
}
