namespace CodeSnifferDog.Models.Scan.Tools.Listing;

/// <summary>
/// Arguments used to list one bounded page of scan-project indexes.
/// </summary>
public sealed class ListProjectsArgs
{
    /// <summary>
    /// Gets the continuation cursor returned by a preceding page, if any.
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Gets the requested number of project indexes to return.
    /// </summary>
    public int? PageSize { get; init; }
}
