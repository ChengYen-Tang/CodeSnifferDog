namespace CodeSnifferDog.Models.RuleReview.Tools.Listing;

/// <summary>
/// Provides the bounded index data needed to select one rule-review issue.
/// </summary>
public sealed class IssueListItem
{
    /// <summary>
    /// Gets the identifier used to retrieve the complete issue.
    /// </summary>
    public required string RuleReviewIssueId { get; init; }

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
