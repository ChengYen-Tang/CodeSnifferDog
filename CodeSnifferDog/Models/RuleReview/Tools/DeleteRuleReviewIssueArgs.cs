namespace CodeSnifferDog.Models.RuleReview.Tools;

/// <summary>
/// Arguments used to delete one stored rule-review issue.
/// </summary>
public sealed class DeleteRuleReviewIssueArgs
{
    /// <summary>
    /// Gets the identifier of the issue to delete.
    /// </summary>
    public required string RuleReviewIssueId { get; init; }
}
