namespace CodeSnifferDog.Models.Report.Tools;

/// <summary>
/// Arguments used to retrieve one stored rule-report issue.
/// </summary>
public sealed class GetRuleReportIssueArgs
{
    /// <summary>
    /// Gets the identifier of the report issue to retrieve.
    /// </summary>
    public required string RuleReportIssueId { get; init; }
}
