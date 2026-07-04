namespace CodeSnifferDog.Models.Scan;

/// <summary>
/// Extends <see cref="ScanProject"/> data with its persisted identifier.
/// </summary>
public sealed class StoredScanProject
{
    /// <summary>
    /// Gets the persistent identifier assigned to the scan project.
    /// </summary>
    public required string ScanProjectId { get; init; }

    /// <summary>
    /// Gets the display name of the project.
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// Gets the repository-relative or absolute path of the project.
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// Gets the project type or platform classification.
    /// </summary>
    public required string ProjectType { get; init; }

    /// <summary>
    /// Gets the reason why the project should be scanned.
    /// </summary>
    public required string Reason { get; init; }
}
