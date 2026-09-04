namespace CodeSnifferDog.Models.ProjectPlan.Tools.Listing;

/// <summary>
/// Represents one bounded page of project-plan task item indexes.
/// </summary>
public sealed class TaskItemPage
{
    /// <summary>
    /// Gets the number of task item indexes returned when no page size is specified.
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Gets the largest number of task item indexes that may be returned in one page.
    /// </summary>
    public const int MaxPageSize = 20;

    /// <summary>
    /// Gets the bounded task item indexes in this page.
    /// </summary>
    public required IReadOnlyList<TaskItemListItem> Items { get; init; }

    /// <summary>
    /// Gets a value indicating whether a subsequent page exists.
    /// </summary>
    public required bool HasMore { get; init; }

    /// <summary>
    /// Gets the continuation cursor for the next page, when <see cref="HasMore"/> is <see langword="true"/>.
    /// </summary>
    public string? NextCursor { get; init; }
}
