namespace CodeSnifferDog.Models.ProjectPlan.Tools;

/// <summary>
/// Arguments used to add one task item to the project-plan store.
/// </summary>
public sealed class AddProjectPlanTaskItemArgs
{
    /// <summary>
    /// Gets the files grouped into the task item.
    /// </summary>
    public required IReadOnlyList<PlanFile> Files { get; init; }
}
