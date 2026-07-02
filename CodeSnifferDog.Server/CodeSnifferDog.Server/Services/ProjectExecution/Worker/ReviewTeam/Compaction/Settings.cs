using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;

internal sealed class Settings
{
    public required OperationalContextCompactionOptions Scan { get; init; }

    public required OperationalContextCompactionOptions ProjectPlan { get; init; }

    public required OperationalContextCompactionOptions RuleReview { get; init; }

    public required OperationalContextCompactionOptions Report { get; init; }
}
