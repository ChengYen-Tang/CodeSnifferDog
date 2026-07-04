namespace CodeSnifferDog.Server.Shared.Reports.Project;

/// <summary>
/// Represents a single report entry in a project report list.
/// </summary>
public sealed class ListItemDto
{
    /// <summary>
    /// Gets the report identifier.
    /// </summary>
    public required Guid ReportId { get; init; }

    /// <summary>
    /// Gets the human-readable rule name.
    /// </summary>
    public required string RuleName { get; init; }
}
