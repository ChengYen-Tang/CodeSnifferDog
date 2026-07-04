using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Models.Report;

/// <summary>
/// Extends <see cref="Issue"/> with its persisted rule-report identifier.
/// </summary>
public sealed class StoredIssue : Issue
{
    /// <summary>
    /// Gets the persistent identifier assigned to the stored report issue.
    /// </summary>
    public required string RuleReportIssueId { get; init; }
}
