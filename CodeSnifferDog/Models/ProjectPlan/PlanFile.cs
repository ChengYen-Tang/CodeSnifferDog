namespace CodeSnifferDog.Models.ProjectPlan;

/// <summary>
/// Describes one file included in a project-plan task item.
/// </summary>
public sealed class PlanFile
{
    /// <summary>
    /// Gets the path of the file to review.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the total line count observed for the file.
    /// </summary>
    public required int TotalLines { get; init; }
}
