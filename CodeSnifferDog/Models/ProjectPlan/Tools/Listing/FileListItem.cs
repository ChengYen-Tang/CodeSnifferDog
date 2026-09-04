namespace CodeSnifferDog.Models.ProjectPlan.Tools.Listing;

/// <summary>
/// Provides the bounded index data needed to identify one project-plan task file.
/// </summary>
public sealed class FileListItem
{
    /// <summary>
    /// Gets a bounded preview of the file path.
    /// </summary>
    public required string FilePathPreview { get; init; }

    /// <summary>
    /// Gets the total line count for the file.
    /// </summary>
    public required int TotalLines { get; init; }
}
