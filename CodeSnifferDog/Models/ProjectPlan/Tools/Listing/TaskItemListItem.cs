namespace CodeSnifferDog.Models.ProjectPlan.Tools.Listing;

/// <summary>
/// Provides the bounded index data needed to identify one project-plan task item.
/// </summary>
public sealed class TaskItemListItem
{
    /// <summary>
    /// Gets the persistent identifier for the stored task item.
    /// </summary>
    public required string ProjectPlanTaskItemId { get; init; }

    /// <summary>
    /// Gets the number of files in the task item.
    /// </summary>
    public required int FileCount { get; init; }

    /// <summary>
    /// Gets the total number of lines across files in the task item.
    /// </summary>
    public required long TotalLines { get; init; }

    /// <summary>
    /// Gets a bounded preview of the first file path in the task item.
    /// </summary>
    public required string FirstFilePathPreview { get; init; }
}
