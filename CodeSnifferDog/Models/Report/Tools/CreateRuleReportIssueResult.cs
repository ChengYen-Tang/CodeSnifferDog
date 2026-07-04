namespace CodeSnifferDog.Models.Report.Tools;

/// <summary>
/// Result returned after creating one stored rule-report issue.
/// </summary>
public sealed class CreateRuleReportIssueResult
{
    /// <summary>
    /// Gets the identifier assigned to the created report issue.
    /// </summary>
    public required string RuleReportIssueId { get; init; }
}
