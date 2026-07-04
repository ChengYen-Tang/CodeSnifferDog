using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;

/// <summary>
/// Creates workflow-stage compaction settings from worker execution options.
/// </summary>
internal sealed class OptionsFactory
{
    /// <summary>
    /// Creates compaction settings for all review workflow stages.
    /// </summary>
    /// <param name="executionOptions">Worker execution options that define compaction behavior.</param>
    /// <returns>The compaction settings shared by each workflow stage.</returns>
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
