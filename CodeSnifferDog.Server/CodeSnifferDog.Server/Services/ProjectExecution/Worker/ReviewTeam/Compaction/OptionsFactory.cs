using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;

internal sealed class OptionsFactory
{
    public Settings Create(ExecutionOptions executionOptions)
    {
        CompactionOptions options = new()
        {
            ModelContextWindowTokens = executionOptions.ModelContextWindowTokens,
            Mode = executionOptions.ContextCompactionMode,
        };

        return new Settings
        {
            Scan = options,
            ProjectPlan = options,
            RuleReview = options,
            Report = options,
        };
    }
}
