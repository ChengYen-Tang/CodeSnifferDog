namespace CodeSnifferDog.Models.ProjectPlan.Tools.Listing;

/// <summary>
/// Represents one bounded page of files for a project-plan task item.
/// </summary>
public sealed class FilePage
{
    /// <summary>
    /// Gets the number of file indexes returned when no page size is specified.
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Gets the largest number of file indexes that may be returned in one page.
    /// </summary>
    public const int MaxPageSize = 20;

    /// <summary>
    /// Gets the task item whose files are represented in this page.
    /// </summary>
    public required string ProjectPlanTaskItemId { get; init; }

    /// <summary>
    /// Gets the bounded file indexes in this page.
    /// </summary>
    public required IReadOnlyList<FileListItem> Items { get; init; }

    /// <summary>
    /// Gets a value indicating whether a subsequent page exists.
    /// </summary>
    public required bool HasMore { get; init; }

    /// <summary>
    /// Gets the zero-based offset for the next page, when <see cref="HasMore"/> is <see langword="true"/>.
    /// </summary>
    public int? NextOffset { get; init; }
}
