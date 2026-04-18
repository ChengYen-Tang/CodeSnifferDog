using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Models.Report;

public sealed class StoredRuleReportIssue : RuleReviewIssue
{
    public required string RuleReportIssueId { get; init; }
}
