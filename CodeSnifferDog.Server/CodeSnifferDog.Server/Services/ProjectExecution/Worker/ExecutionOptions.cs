using TeamExecutionOptions = CodeSnifferDog.Models.ReviewAgentTeam.Runtime.ExecutionOptions;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

/// <summary>
/// Configures how the review-team worker executes project analysis.
/// </summary>
public sealed class ExecutionOptions
{
    /// <summary>
    /// Gets the maximum number of agents that may run in parallel.
    /// </summary>
    public int MaxParallelAgents { get; init; } = 2;

    /// <summary>
    /// Gets the model context window size, in tokens, used by the worker.
    /// </summary>
    public long ModelContextWindowTokens { get; init; } =
        TeamExecutionOptions.DefaultModelContextWindowTokens;

    /// <summary>
    /// Gets the context compaction mode applied to review-team agents.
    /// </summary>
    public CompactionMode ContextCompactionMode { get; init; } =
        CompactionMode.Standard;

    /// <summary>
    /// Gets the agent run timeout, in seconds.
    /// </summary>
    public int AgentRunTimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Gets the maximum number of consecutive agent run failures allowed.
    /// </summary>
    public int MaxConsecutiveAgentRunFailures { get; init; } = 5;

    /// <summary>
    /// Gets the maximum number of missing-submission retries allowed.
    /// </summary>
    public int MaxMissingSubmissionAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the maximum number of verifier rejection retries allowed.
    /// </summary>
    public int MaxVerifierRejectionAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the normalized agent run timeout.
    /// </summary>
    public TimeSpan AgentRunTimeout => TimeSpan.FromSeconds(Math.Max(1, AgentRunTimeoutSeconds));
}
