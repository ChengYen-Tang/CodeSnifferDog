namespace CodeSnifferDog.Models.Scan.Tools;

/// <summary>
/// Arguments used to add multiple projects to the scan store.
/// </summary>
public sealed class AddScanProjectsArgs
{
    /// <summary>
    /// Gets the projects to add.
    /// </summary>
    public required IReadOnlyList<AddScanProjectArgs> Projects { get; init; }
}
