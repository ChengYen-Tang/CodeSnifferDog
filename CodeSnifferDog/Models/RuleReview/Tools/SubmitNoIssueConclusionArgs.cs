namespace CodeSnifferDog.Models.RuleReview.Tools;

/// <summary>
/// Arguments used to submit a no-issue conclusion for a rule review.
/// </summary>
public sealed class SubmitNoIssueConclusionArgs
{
    /// <summary>
    /// Gets the review strategy used before reaching the no-issue conclusion.
    /// </summary>
    public required string ReviewStrategy { get; init; }

    /// <summary>
    /// Gets the statement describing what review scope was covered.
    /// </summary>
    public required string ScopeCoverage { get; init; }

    /// <summary>
    /// Gets the cross-scope analysis that supports the conclusion.
    /// </summary>
    public required string CrossScopeAnalysis { get; init; }

    /// <summary>
    /// Gets the explanation of why no issue was found.
    /// </summary>
    public required string WhyNoIssueWasFound { get; init; }
}
