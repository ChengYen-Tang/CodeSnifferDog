namespace CodeSnifferDog.Models.Scan.Tools.Listing;

/// <summary>
/// Represents one bounded page of scan-project indexes.
/// </summary>
public sealed class ProjectPage
{
    /// <summary>
    /// Gets the number of project indexes returned when no page size is specified.
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Gets the largest number of project indexes that may be returned in one page.
    /// </summary>
    public const int MaxPageSize = 20;

    /// <summary>
    /// Gets the bounded project indexes in this page.
    /// </summary>
    public required IReadOnlyList<ProjectListItem> Items { get; init; }

    /// <summary>
    /// Gets a value indicating whether a subsequent page exists.
    /// </summary>
    public required bool HasMore { get; init; }

    /// <summary>
    /// Gets the continuation cursor for the next page, when <see cref="HasMore"/> is <see langword="true"/>.
    /// </summary>
    public string? NextCursor { get; init; }
}
