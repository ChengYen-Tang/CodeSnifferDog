namespace CodeSnifferDog.Models.RuleReview.Tools;

/// <summary>
/// Arguments used to retrieve one stored rule-review issue.
/// </summary>
public sealed class GetRuleReviewIssueArgs
{
    /// <summary>
    /// Gets the identifier of the issue to retrieve.
    /// </summary>
    public required string RuleReviewIssueId { get; init; }
}
