namespace CodeSnifferDog.Models.Scan.Tools;

/// <summary>
/// Result returned after adding one project to the scan store.
/// </summary>
public sealed class AddScanProjectResult
{
    /// <summary>
    /// Gets the identifier assigned to the created scan project.
    /// </summary>
    public required string ScanProjectId { get; init; }
}
