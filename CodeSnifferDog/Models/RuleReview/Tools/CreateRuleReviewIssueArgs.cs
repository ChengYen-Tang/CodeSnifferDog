namespace CodeSnifferDog.Models.RuleReview.Tools;

/// <summary>
/// Arguments used to create one stored rule-review issue.
/// </summary>
public sealed class CreateRuleReviewIssueArgs
{
    /// <summary>
    /// Gets the issue category or rule-specific issue type.
    /// </summary>
    public required string IssueType { get; init; }

    /// <summary>
    /// Gets the normalized severity label for the issue.
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// Gets the primary file or function where the issue was observed.
    /// </summary>
    public required string FileOrFunction { get; init; }

    /// <summary>
    /// Gets the relevant code pattern or expression that supports the finding.
    /// </summary>
    public required string RelevantCodePatternOrExpression { get; init; }

    /// <summary>
    /// Gets the explanation of why the finding is a problem.
    /// </summary>
    public required string WhyThisIsAProblem { get; init; }

    /// <summary>
    /// Gets the confidence statement for the finding.
    /// </summary>
    public required string Confidence { get; init; }

    /// <summary>
    /// Gets follow-up files that should be inspected together with the finding.
    /// </summary>
    public required string FollowUpFiles { get; init; }

    /// <summary>
    /// Gets the suggested direction for fixing or mitigating the issue.
    /// </summary>
    public required string SuggestedFixDirection { get; init; }

    /// <summary>
    /// Gets the statement describing what review scope was covered.
    /// </summary>
    public required string ScopeCoverage { get; init; }

    /// <summary>
    /// Gets the cross-scope analysis that supports the finding.
    /// </summary>
    public required string CrossScopeAnalysis { get; init; }

    /// <summary>
    /// Gets the review strategy that produced the finding.
    /// </summary>
    public required string ReviewStrategy { get; init; }
}
