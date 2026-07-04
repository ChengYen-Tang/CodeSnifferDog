namespace CodeSnifferDog.Models.ProjectPlan;

/// <summary>
/// Describes one project-plan task item before it is persisted.
/// </summary>
public sealed class TaskItem
{
    /// <summary>
    /// Gets the files grouped into the task item.
    /// </summary>
    public required IReadOnlyList<PlanFile> Files { get; init; }
}
