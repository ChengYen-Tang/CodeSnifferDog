using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal sealed class ProjectReviewAgentCompactionSettings
{
    public required OperationalContextCompactionOptions Scan { get; init; }

    public required OperationalContextCompactionOptions ProjectPlan { get; init; }

    public required OperationalContextCompactionOptions RuleReview { get; init; }

    public required OperationalContextCompactionOptions Report { get; init; }
}
