namespace CodeSnifferDog.Models.Scan.Tools.Listing;

/// <summary>
/// Provides the bounded index data needed to identify one scan project.
/// </summary>
public sealed class ProjectListItem
{
    /// <summary>
    /// Gets the persistent identifier for the stored scan project.
    /// </summary>
    public required string ScanProjectId { get; init; }

    /// <summary>
    /// Gets a bounded preview of the project display name.
    /// </summary>
    public required string ProjectNamePreview { get; init; }

    /// <summary>
    /// Gets a bounded preview of the project path.
    /// </summary>
    public required string ProjectPathPreview { get; init; }

    /// <summary>
    /// Gets a bounded preview of the project type.
    /// </summary>
    public required string ProjectTypePreview { get; init; }

    /// <summary>
    /// Gets a bounded preview of the reason that the project was selected.
    /// </summary>
    public required string ReasonPreview { get; init; }
}
