namespace CodeSnifferDog.Models.ProjectPlan.Tools;

/// <summary>
/// Arguments used to add multiple task items to the project-plan store.
/// </summary>
public sealed class AddProjectPlanTaskItemsArgs
{
    /// <summary>
    /// Gets the task items to add.
    /// </summary>
    public required IReadOnlyList<AddProjectPlanTaskItemArgs> TaskItems { get; init; }
}
