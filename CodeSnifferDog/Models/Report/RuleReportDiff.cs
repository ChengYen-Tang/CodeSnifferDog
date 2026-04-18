namespace CodeSnifferDog.Models.Report;

public sealed class RuleReportDiff
{
    public required IReadOnlyList<StoredRuleReportIssue> CreatedIssues { get; init; }

    public required IReadOnlyList<StoredRuleReportIssue> UpdatedIssues { get; init; }

    public required IReadOnlyList<StoredRuleReportIssue> DeletedIssues { get; init; }
}
