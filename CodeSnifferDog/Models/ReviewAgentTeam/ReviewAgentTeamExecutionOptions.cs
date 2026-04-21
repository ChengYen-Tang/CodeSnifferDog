using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Models.ReviewAgentTeam;

public sealed class ReviewAgentTeamExecutionOptions
{
    public const long DefaultModelContextWindowTokens = 128_000;

    public required int MaxParallelAgents { get; init; }

    public long ModelContextWindowTokens { get; init; } = DefaultModelContextWindowTokens;

    public OperationalContextCompactionMode ContextCompactionMode { get; init; } =
        OperationalContextCompactionMode.Standard;
}
