namespace CodeSnifferDog.Models.Report.Tools;

/// <summary>
/// Arguments used to delete one stored rule-report issue.
/// </summary>
public sealed class DeleteRuleReportIssueArgs
{
    /// <summary>
    /// Gets the identifier of the report issue to delete.
    /// </summary>
    public required string RuleReportIssueId { get; init; }
}
