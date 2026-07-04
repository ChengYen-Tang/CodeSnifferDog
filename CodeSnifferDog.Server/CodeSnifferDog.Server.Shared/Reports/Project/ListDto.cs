namespace CodeSnifferDog.Server.Shared.Reports.Project;

/// <summary>
/// Carries the list of reports available for a project.
/// </summary>
public sealed class ListDto
{
    /// <summary>
    /// Gets the original uploaded file name.
    /// </summary>
    public required string OriginalFileName { get; init; }

    /// <summary>
    /// Gets the available report summaries.
    /// </summary>
    public required IReadOnlyList<ListItemDto> Reports { get; init; }
}
