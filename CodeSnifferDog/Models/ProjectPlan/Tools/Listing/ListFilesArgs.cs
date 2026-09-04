namespace CodeSnifferDog.Models.ProjectPlan.Tools.Listing;

/// <summary>
/// Arguments used to list one bounded page of files for a project-plan task item.
/// </summary>
public sealed class ListFilesArgs
{
    /// <summary>
    /// Gets the identifier of the stored task item whose files should be listed.
    /// </summary>
    public required string ProjectPlanTaskItemId { get; init; }

    /// <summary>
    /// Gets the zero-based file offset returned by a preceding page, if any.
    /// </summary>
    public int? Offset { get; init; }

    /// <summary>
    /// Gets the requested number of file indexes to return.
    /// </summary>
    public int? PageSize { get; init; }
}
