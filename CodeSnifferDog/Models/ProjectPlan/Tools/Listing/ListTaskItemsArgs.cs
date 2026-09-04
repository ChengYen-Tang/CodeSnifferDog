namespace CodeSnifferDog.Models.ProjectPlan.Tools.Listing;

/// <summary>
/// Arguments used to list one bounded page of project-plan task item indexes.
/// </summary>
public sealed class ListTaskItemsArgs
{
    /// <summary>
    /// Gets the continuation cursor returned by a preceding page, if any.
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Gets the requested number of task item indexes to return.
    /// </summary>
    public int? PageSize { get; init; }
}
