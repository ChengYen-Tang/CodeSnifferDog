namespace CodeSnifferDog.Models.RuleReview;

public sealed class NoIssueConclusion
{
    public required string ReviewStrategy { get; init; }

    public required string ScopeCoverage { get; init; }

    public required string CrossScopeAnalysis { get; init; }

    public required string WhyNoIssueWasFound { get; init; }
}
