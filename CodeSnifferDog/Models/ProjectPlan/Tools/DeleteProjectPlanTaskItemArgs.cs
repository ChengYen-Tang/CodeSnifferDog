namespace CodeSnifferDog.Models.ProjectPlan.Tools;

/// <summary>
/// Arguments used to delete one task item from the project-plan store.
/// </summary>
public sealed class DeleteProjectPlanTaskItemArgs
{
    /// <summary>
    /// Gets the identifier of the task item to delete.
    /// </summary>
    public required string ProjectPlanTaskItemId { get; init; }
}
