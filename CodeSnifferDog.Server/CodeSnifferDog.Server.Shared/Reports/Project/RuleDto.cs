namespace CodeSnifferDog.Server.Shared.Reports.Project;

/// <summary>
/// Represents a rule report included in a project report bundle.
/// </summary>
public sealed class RuleDto
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
