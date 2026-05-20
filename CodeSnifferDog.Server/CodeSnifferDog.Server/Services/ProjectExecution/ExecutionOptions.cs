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

    public int AgentRunTimeoutSeconds { get; init; } = 300;

    public int MaxConsecutiveAgentRunFailures { get; init; } = 5;

    public TimeSpan AgentRunTimeout => TimeSpan.FromSeconds(Math.Max(1, AgentRunTimeoutSeconds));
}
