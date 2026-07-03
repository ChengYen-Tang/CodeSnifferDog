using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;

internal sealed class Settings
{
    public required CompactionOptions Scan { get; init; }

    public required CompactionOptions ProjectPlan { get; init; }

    public required CompactionOptions RuleReview { get; init; }

    public required CompactionOptions Report { get; init; }
}
