namespace CodeSnifferDog.Models.RuleReview;

/// <summary>
/// Extends <see cref="Issue"/> with its persisted identifier.
/// </summary>
public sealed class StoredIssue : Issue
{
    /// <summary>
    /// Gets the persistent identifier assigned to the stored issue.
    /// </summary>
    public required string RuleReviewIssueId { get; init; }
}
