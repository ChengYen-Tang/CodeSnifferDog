namespace CodeSnifferDog.Models.Scan.Tools;

/// <summary>
/// Arguments used to add one project to the scan store.
/// </summary>
public sealed class AddScanProjectArgs
{
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
