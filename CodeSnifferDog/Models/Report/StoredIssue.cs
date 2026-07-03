using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Models.Report;

public sealed class StoredIssue : Issue
{
    public required string RuleReportIssueId { get; init; }
}
