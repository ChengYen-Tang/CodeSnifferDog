namespace CodeSnifferDog.Models.RuleReview.Tools;

/// <summary>
/// Result returned after creating one stored rule-review issue.
/// </summary>
public sealed class CreateRuleReviewIssueResult
{
    /// <summary>
    /// Gets the identifier assigned to the created issue.
    /// </summary>
    public required string RuleReviewIssueId { get; init; }
}
