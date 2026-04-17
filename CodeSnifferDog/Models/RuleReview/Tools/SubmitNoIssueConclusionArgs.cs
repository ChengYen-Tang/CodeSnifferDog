namespace CodeSnifferDog.Models.RuleReview.Tools;

public sealed class SubmitNoIssueConclusionArgs
{
    public required string ReviewStrategy { get; init; }

    public required string ScopeCoverage { get; init; }

    public required string CrossScopeAnalysis { get; init; }

    public required string WhyNoIssueWasFound { get; init; }
}
