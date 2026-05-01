using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class ExecutionOptions
{
    public int MaxParallelAgents { get; init; } = 2;

    public long ModelContextWindowTokens { get; init; } =
        ReviewAgentTeamExecutionOptions.DefaultModelContextWindowTokens;

    public OperationalContextCompactionMode ContextCompactionMode { get; init; } =
        OperationalContextCompactionMode.Standard;
}
