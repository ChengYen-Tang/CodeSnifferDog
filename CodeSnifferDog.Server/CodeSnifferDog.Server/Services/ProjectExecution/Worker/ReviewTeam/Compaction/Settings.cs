using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;

/// <summary>
/// Holds the compaction options used by each review workflow stage.
/// </summary>
internal sealed class Settings
{
    /// <summary>
    /// Gets the compaction options for the scan stage.
    /// </summary>
    public required CompactionOptions Scan { get; init; }

    /// <summary>
    /// Gets the compaction options for the project-plan stage.
    /// </summary>
    public required CompactionOptions ProjectPlan { get; init; }

    /// <summary>
    /// Gets the compaction options for the rule-review stage.
    /// </summary>
    public required CompactionOptions RuleReview { get; init; }

    /// <summary>
    /// Gets the compaction options for the report stage.
    /// </summary>
    public required CompactionOptions Report { get; init; }
}
