namespace CodeSnifferDog.Models.Scan.Tools;

/// <summary>
/// Arguments used to delete one project from the scan store.
/// </summary>
public sealed class DeleteScanProjectArgs
{
    /// <summary>
    /// Gets the identifier of the project to delete.
    /// </summary>
    public required string ScanProjectId { get; init; }
}
