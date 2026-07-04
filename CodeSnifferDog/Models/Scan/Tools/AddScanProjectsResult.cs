namespace CodeSnifferDog.Models.Scan.Tools;

/// <summary>
/// Result returned after adding multiple projects to the scan store.
/// </summary>
public sealed class AddScanProjectsResult
{
    /// <summary>
    /// Gets the identifiers assigned to the created scan projects.
    /// </summary>
    public required IReadOnlyList<string> ScanProjectIds { get; init; }
}
