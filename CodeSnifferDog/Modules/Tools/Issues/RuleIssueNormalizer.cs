using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.Issues;

internal static class RuleIssueNormalizer
{
    public static RuleReviewIssue Create(
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
        Normalize(new RuleReviewIssue
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
        NormalizeToContract(new RuleReviewIssue
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

    public static RuleReviewIssue Normalize(RuleReviewIssue issue)
        =>
        NormalizeToContract(issue).Issue;

    public static NormalizedRuleIssue NormalizeToContract(RuleReviewIssue issue)
    {
        Validate(issue);
        return new NormalizedRuleIssue(new RuleReviewIssue
        {
            IssueType = issue.IssueType.Trim(),
            Severity = RuleReviewSeverity.Normalize(issue.Severity),
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

    private static void Validate(RuleReviewIssue issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.IssueType);
        RuleReviewSeverity.Normalize(issue.Severity);
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
