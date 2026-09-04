namespace CodeSnifferDog.Models.Report.Tools.Listing;

/// <summary>
/// Provides the bounded index data needed to select one repository-level rule report issue.
/// </summary>
public sealed class IssueListItem
{
    /// <summary>
    /// Gets the identifier used to retrieve the complete issue.
    /// </summary>
    public required string RuleReportIssueId { get; init; }

    /// <summary>
    /// Gets the normalized severity label for the issue.
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// Gets a bounded preview of the issue type.
    /// </summary>
    public required string IssueTypePreview { get; init; }

    /// <summary>
    /// Gets a bounded preview of the file or function where the issue was found.
    /// </summary>
    public required string LocationPreview { get; init; }
}
