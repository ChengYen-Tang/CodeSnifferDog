namespace CodeSnifferDog.Models.ReviewStage;

/// <summary>
/// Holds the full review-stage output across all planned projects.
/// </summary>
public sealed class WorkflowResult
{
    /// <summary>
    /// Gets the per-project review-stage results.
    /// </summary>
    public required IReadOnlyList<ProjectResult> ProjectResults { get; init; }
}
