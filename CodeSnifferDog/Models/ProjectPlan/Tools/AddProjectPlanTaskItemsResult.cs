namespace CodeSnifferDog.Models.ProjectPlan.Tools;

/// <summary>
/// Result returned after adding multiple task items to the project-plan store.
/// </summary>
public sealed class AddProjectPlanTaskItemsResult
{
    /// <summary>
    /// Gets the identifiers assigned to the created task items.
    /// </summary>
    public required IReadOnlyList<string> ProjectPlanTaskItemIds { get; init; }
}
