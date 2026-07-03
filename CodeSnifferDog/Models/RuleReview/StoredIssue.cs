namespace CodeSnifferDog.Models.RuleReview;

public sealed class StoredIssue : Issue
{
    public required string RuleReviewIssueId { get; init; }
}
