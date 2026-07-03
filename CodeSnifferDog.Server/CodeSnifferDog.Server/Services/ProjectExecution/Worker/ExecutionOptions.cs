using TeamExecutionOptions = CodeSnifferDog.Models.ReviewAgentTeam.Runtime.ExecutionOptions;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

public sealed class ExecutionOptions
{
    public int MaxParallelAgents { get; init; } = 2;

    public long ModelContextWindowTokens { get; init; } =
        TeamExecutionOptions.DefaultModelContextWindowTokens;

    public CompactionMode ContextCompactionMode { get; init; } =
        CompactionMode.Standard;

    public int AgentRunTimeoutSeconds { get; init; } = 300;

    public int MaxConsecutiveAgentRunFailures { get; init; } = 5;

    public TimeSpan AgentRunTimeout => TimeSpan.FromSeconds(Math.Max(1, AgentRunTimeoutSeconds));
}
