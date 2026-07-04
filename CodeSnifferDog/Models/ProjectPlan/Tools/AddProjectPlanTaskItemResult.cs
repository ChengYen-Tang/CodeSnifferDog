namespace CodeSnifferDog.Models.ProjectPlan.Tools;

/// <summary>
/// Result returned after adding one task item to the project-plan store.
/// </summary>
public sealed class AddProjectPlanTaskItemResult
{
    /// <summary>
    /// Gets the identifier assigned to the created task item.
    /// </summary>
    public required string ProjectPlanTaskItemId { get; init; }
}
