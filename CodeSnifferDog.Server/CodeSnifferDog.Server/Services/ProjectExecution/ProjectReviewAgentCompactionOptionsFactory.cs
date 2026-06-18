using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal sealed class ProjectReviewAgentCompactionOptionsFactory
{
    public ProjectReviewAgentCompactionSettings Create(ExecutionOptions executionOptions)
    {
        OperationalContextCompactionOptions options = new()
        {
            ModelContextWindowTokens = executionOptions.ModelContextWindowTokens,
            Mode = executionOptions.ContextCompactionMode,
        };

        return new ProjectReviewAgentCompactionSettings
        {
            Scan = options,
            ProjectPlan = options,
            RuleReview = options,
            Report = options,
        };
    }
}
