namespace CodeSnifferDog.Server.Shared.Reports.Project;

/// <summary>
/// Carries the full set of rule reports for a project.
/// </summary>
public sealed class BundleDto
{
    /// <summary>
    /// Gets the original uploaded file name.
    /// </summary>
    public required string OriginalFileName { get; init; }

    /// <summary>
    /// Gets the complete set of rule reports.
    /// </summary>
    public required IReadOnlyList<RuleDto> Reports { get; init; }
}
