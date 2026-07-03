using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Models.ReviewAgentTeam.Runtime;

public sealed class ExecutionOptions
{
    public const long DefaultModelContextWindowTokens = 128_000;

    public required int MaxParallelAgents { get; init; }

    public long ModelContextWindowTokens { get; init; } = DefaultModelContextWindowTokens;

    public CompactionMode ContextCompactionMode { get; init; } =
        CompactionMode.Standard;
}
