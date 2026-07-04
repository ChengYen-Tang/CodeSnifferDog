using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.Issues;

/// <summary>
/// Normalizes rule issues into the canonical stored representation.
/// </summary>
internal static class RuleIssueNormalizer
{
    /// <summary>
    /// Creates a normalized <see cref="Issue" /> from raw issue fields.
    /// </summary>
    public static Issue Create(
        string issueType,
        string severity,
        string fileOrFunction,
        string relevantCodePatternOrExpression,
        string whyThisIsAProblem,
        string confidence,
        string followUpFiles,
        string suggestedFixDirection,
        string scopeCoverage,
        string crossScopeAnalysis,
        string reviewStrategy) =>
        Normalize(new Issue
        {
            IssueType = issueType,
            Severity = severity,
            FileOrFunction = fileOrFunction,
            RelevantCodePatternOrExpression = relevantCodePatternOrExpression,
            WhyThisIsAProblem = whyThisIsAProblem,
            Confidence = confidence,
            FollowUpFiles = followUpFiles,
            SuggestedFixDirection = suggestedFixDirection,
            ScopeCoverage = scopeCoverage,
            CrossScopeAnalysis = crossScopeAnalysis,
            ReviewStrategy = reviewStrategy,
        });

    /// <summary>
    /// Creates a normalized rule-issue contract from raw issue fields.
    /// </summary>
    public static NormalizedRuleIssue CreateContract(
        string issueType,
        string severity,
        string fileOrFunction,
        string relevantCodePatternOrExpression,
        string whyThisIsAProblem,
        string confidence,
        string followUpFiles,
        string suggestedFixDirection,
        string scopeCoverage,
        string crossScopeAnalysis,
        string reviewStrategy) =>
        NormalizeToContract(new Issue
        {
            IssueType = issueType,
            Severity = severity,
            FileOrFunction = fileOrFunction,
            RelevantCodePatternOrExpression = relevantCodePatternOrExpression,
            WhyThisIsAProblem = whyThisIsAProblem,
            Confidence = confidence,
            FollowUpFiles = followUpFiles,
            SuggestedFixDirection = suggestedFixDirection,
            ScopeCoverage = scopeCoverage,
            CrossScopeAnalysis = crossScopeAnalysis,
            ReviewStrategy = reviewStrategy,
        });

    /// <summary>
    /// Normalizes one issue into its canonical stored form.
    /// </summary>
    /// <param name="issue">Issue to normalize.</param>
    /// <returns>The normalized issue.</returns>
    public static Issue Normalize(Issue issue)
        =>
        NormalizeToContract(issue).Issue;

    /// <summary>
    /// Normalizes one issue and keeps it wrapped in a comparison contract.
    /// </summary>
    /// <param name="issue">Issue to normalize.</param>
    /// <returns>The normalized issue contract.</returns>
    public static NormalizedRuleIssue NormalizeToContract(Issue issue)
    {
        Validate(issue);
        return new NormalizedRuleIssue(new Issue
        {
            IssueType = issue.IssueType.Trim(),
            Severity = Severity.Normalize(issue.Severity),
            FileOrFunction = issue.FileOrFunction.Trim(),
            RelevantCodePatternOrExpression = issue.RelevantCodePatternOrExpression.Trim(),
            WhyThisIsAProblem = issue.WhyThisIsAProblem.Trim(),
            Confidence = issue.Confidence.Trim(),
            FollowUpFiles = issue.FollowUpFiles.Trim(),
            SuggestedFixDirection = issue.SuggestedFixDirection.Trim(),
            ReviewStrategy = issue.ReviewStrategy.Trim(),
            ScopeCoverage = issue.ScopeCoverage.Trim(),
            CrossScopeAnalysis = issue.CrossScopeAnalysis.Trim(),
        });
    }

    /// <summary>
    /// Validates that one issue contains all required fields.
    /// </summary>
    /// <param name="issue">Issue to validate.</param>
    private static void Validate(Issue issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.IssueType);
        Severity.Normalize(issue.Severity);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.FileOrFunction);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.RelevantCodePatternOrExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.WhyThisIsAProblem);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.Confidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.FollowUpFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.SuggestedFixDirection);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.ReviewStrategy);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.ScopeCoverage);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.CrossScopeAnalysis);
    }
}
