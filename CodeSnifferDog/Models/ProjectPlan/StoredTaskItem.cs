namespace CodeSnifferDog.Models.ProjectPlan;

/// <summary>
/// Extends a project-plan task item with its persisted identifier.
/// </summary>
public sealed class StoredTaskItem
{
    /// <summary>
    /// Gets the persistent identifier assigned to the task item.
    /// </summary>
    public required string ProjectPlanTaskItemId { get; init; }

    /// <summary>
    /// Gets the files grouped into the task item.
    /// </summary>
    public required IReadOnlyList<PlanFile> Files { get; init; }
}
