namespace CodeSnifferDog.Models.Report.Tools.Listing;

/// <summary>
/// Arguments used to list one bounded page of repository-level rule report issue indexes.
/// </summary>
public sealed class ListIssuesArgs
{
    /// <summary>
    /// Gets the continuation cursor returned by a preceding page, if any.
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Gets the requested number of issue indexes to return.
    /// </summary>
    public int? PageSize { get; init; }
}
