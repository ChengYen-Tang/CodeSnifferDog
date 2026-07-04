namespace CodeSnifferDog.Server.Shared.Reports.Project;

/// <summary>
/// Carries the full content of a single rule report.
/// </summary>
public sealed class ContentDto
{
    /// <summary>
    /// Gets the report identifier.
    /// </summary>
    public required Guid ReportId { get; init; }

    /// <summary>
    /// Gets the human-readable rule name.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Gets the markdown report content.
    /// </summary>
    public required string MarkdownContent { get; init; }
}
