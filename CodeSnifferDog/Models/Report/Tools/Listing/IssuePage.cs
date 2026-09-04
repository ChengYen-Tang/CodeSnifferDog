namespace CodeSnifferDog.Models.Report.Tools.Listing;

/// <summary>
/// Represents one bounded page of repository-level rule report issue indexes.
/// </summary>
public sealed class IssuePage
{
    /// <summary>
    /// Gets the number of issue indexes returned when no page size is specified.
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Gets the largest number of issue indexes that may be returned in one page.
    /// </summary>
    public const int MaxPageSize = 20;

    /// <summary>
    /// Gets the bounded issue indexes in this page.
    /// </summary>
    public required IReadOnlyList<IssueListItem> Items { get; init; }

    /// <summary>
    /// Gets a value indicating whether a subsequent page exists.
    /// </summary>
    public required bool HasMore { get; init; }

    /// <summary>
    /// Gets the continuation cursor for the next page, when <see cref="HasMore"/> is <see langword="true"/>.
    /// </summary>
    public string? NextCursor { get; init; }
}
