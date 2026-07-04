using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Models.ReviewAgentTeam.Runtime;

/// <summary>
/// Configures concurrency and compaction behavior for review-agent execution.
/// </summary>
public sealed class ExecutionOptions
{
    /// <summary>
    /// Default model context window used when the caller does not specify one explicitly.
    /// </summary>
    public const long DefaultModelContextWindowTokens = 128_000;

    /// <summary>
    /// Gets the maximum number of agents allowed to run in parallel.
    /// </summary>
    public required int MaxParallelAgents { get; init; }

    /// <summary>
    /// Gets the model context window used to choose compaction behavior.
    /// </summary>
    public long ModelContextWindowTokens { get; init; } = DefaultModelContextWindowTokens;

    /// <summary>
    /// Gets the context compaction mode used by created agents.
    /// </summary>
    public CompactionMode ContextCompactionMode { get; init; } =
        CompactionMode.Standard;
}
