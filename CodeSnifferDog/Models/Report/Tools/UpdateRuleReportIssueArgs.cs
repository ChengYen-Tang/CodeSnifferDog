namespace CodeSnifferDog.Models.Report.Tools;

public sealed class UpdateRuleReportIssueArgs
{
    public required string RuleReportIssueId { get; init; }

    public required string IssueType { get; init; }

    public required string Severity { get; init; }

    public required string FileOrFunction { get; init; }

    public required string RelevantCodePatternOrExpression { get; init; }

    public required string WhyThisIsAProblem { get; init; }

    public required string Confidence { get; init; }

    public required string FollowUpFiles { get; init; }

    public required string SuggestedFixDirection { get; init; }

    public required string ScopeCoverage { get; init; }

    public required string CrossScopeAnalysis { get; init; }

    public required string ReviewStrategy { get; init; }
}
