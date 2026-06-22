using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.Issues;

internal static class RuleIssueStoreMapper
{
    public static StoredRuleReviewIssue CreateReviewIssue(RuleReviewIssue normalizedIssue, string id) => new()
    {
        RuleReviewIssueId = id,
        IssueType = normalizedIssue.IssueType,
        Severity = normalizedIssue.Severity,
        FileOrFunction = normalizedIssue.FileOrFunction,
        RelevantCodePatternOrExpression = normalizedIssue.RelevantCodePatternOrExpression,
        WhyThisIsAProblem = normalizedIssue.WhyThisIsAProblem,
        Confidence = normalizedIssue.Confidence,
        FollowUpFiles = normalizedIssue.FollowUpFiles,
        SuggestedFixDirection = normalizedIssue.SuggestedFixDirection,
        ReviewStrategy = normalizedIssue.ReviewStrategy,
        ScopeCoverage = normalizedIssue.ScopeCoverage,
        CrossScopeAnalysis = normalizedIssue.CrossScopeAnalysis,
    };

    public static StoredRuleReportIssue CreateReportIssue(RuleReviewIssue normalizedIssue, string id) => new()
    {
        RuleReportIssueId = id,
        IssueType = normalizedIssue.IssueType,
        Severity = normalizedIssue.Severity,
        FileOrFunction = normalizedIssue.FileOrFunction,
        RelevantCodePatternOrExpression = normalizedIssue.RelevantCodePatternOrExpression,
        WhyThisIsAProblem = normalizedIssue.WhyThisIsAProblem,
        Confidence = normalizedIssue.Confidence,
        FollowUpFiles = normalizedIssue.FollowUpFiles,
        SuggestedFixDirection = normalizedIssue.SuggestedFixDirection,
        ReviewStrategy = normalizedIssue.ReviewStrategy,
        ScopeCoverage = normalizedIssue.ScopeCoverage,
        CrossScopeAnalysis = normalizedIssue.CrossScopeAnalysis,
    };

    public static StoredRuleReviewIssue Clone(StoredRuleReviewIssue issue) => new()
    {
        RuleReviewIssueId = issue.RuleReviewIssueId,
        IssueType = issue.IssueType,
        Severity = issue.Severity,
        FileOrFunction = issue.FileOrFunction,
        RelevantCodePatternOrExpression = issue.RelevantCodePatternOrExpression,
        WhyThisIsAProblem = issue.WhyThisIsAProblem,
        Confidence = issue.Confidence,
        FollowUpFiles = issue.FollowUpFiles,
        SuggestedFixDirection = issue.SuggestedFixDirection,
        ReviewStrategy = issue.ReviewStrategy,
        ScopeCoverage = issue.ScopeCoverage,
        CrossScopeAnalysis = issue.CrossScopeAnalysis,
    };

    public static StoredRuleReportIssue Clone(StoredRuleReportIssue issue) => new()
    {
        RuleReportIssueId = issue.RuleReportIssueId,
        IssueType = issue.IssueType,
        Severity = issue.Severity,
        FileOrFunction = issue.FileOrFunction,
        RelevantCodePatternOrExpression = issue.RelevantCodePatternOrExpression,
        WhyThisIsAProblem = issue.WhyThisIsAProblem,
        Confidence = issue.Confidence,
        FollowUpFiles = issue.FollowUpFiles,
        SuggestedFixDirection = issue.SuggestedFixDirection,
        ReviewStrategy = issue.ReviewStrategy,
        ScopeCoverage = issue.ScopeCoverage,
        CrossScopeAnalysis = issue.CrossScopeAnalysis,
    };

    public static bool IsEquivalentToNormalizedIssue(StoredRuleReviewIssue storedIssue, RuleReviewIssue normalizedIssue) =>
        string.Equals(storedIssue.IssueType, normalizedIssue.IssueType, StringComparison.Ordinal) &&
        string.Equals(storedIssue.Severity, normalizedIssue.Severity, StringComparison.Ordinal) &&
        string.Equals(storedIssue.FileOrFunction, normalizedIssue.FileOrFunction, StringComparison.Ordinal) &&
        string.Equals(storedIssue.RelevantCodePatternOrExpression, normalizedIssue.RelevantCodePatternOrExpression, StringComparison.Ordinal) &&
        string.Equals(storedIssue.WhyThisIsAProblem, normalizedIssue.WhyThisIsAProblem, StringComparison.Ordinal) &&
        string.Equals(storedIssue.Confidence, normalizedIssue.Confidence, StringComparison.Ordinal) &&
        string.Equals(storedIssue.FollowUpFiles, normalizedIssue.FollowUpFiles, StringComparison.Ordinal) &&
        string.Equals(storedIssue.SuggestedFixDirection, normalizedIssue.SuggestedFixDirection, StringComparison.Ordinal) &&
        string.Equals(storedIssue.ReviewStrategy, normalizedIssue.ReviewStrategy, StringComparison.Ordinal) &&
        string.Equals(storedIssue.ScopeCoverage, normalizedIssue.ScopeCoverage, StringComparison.Ordinal) &&
        string.Equals(storedIssue.CrossScopeAnalysis, normalizedIssue.CrossScopeAnalysis, StringComparison.Ordinal);

    public static bool IsEquivalentToIssue(StoredRuleReportIssue storedIssue, RuleReviewIssue issue)
    {
        RuleReviewIssue normalizedIssue = RuleIssueNormalizer.Normalize(issue);
        return string.Equals(storedIssue.IssueType, normalizedIssue.IssueType, StringComparison.Ordinal) &&
            string.Equals(storedIssue.Severity, normalizedIssue.Severity, StringComparison.Ordinal) &&
            string.Equals(storedIssue.FileOrFunction, normalizedIssue.FileOrFunction, StringComparison.Ordinal) &&
            string.Equals(storedIssue.RelevantCodePatternOrExpression, normalizedIssue.RelevantCodePatternOrExpression, StringComparison.Ordinal) &&
            string.Equals(storedIssue.WhyThisIsAProblem, normalizedIssue.WhyThisIsAProblem, StringComparison.Ordinal) &&
            string.Equals(storedIssue.Confidence, normalizedIssue.Confidence, StringComparison.Ordinal) &&
            string.Equals(storedIssue.FollowUpFiles, normalizedIssue.FollowUpFiles, StringComparison.Ordinal) &&
            string.Equals(storedIssue.SuggestedFixDirection, normalizedIssue.SuggestedFixDirection, StringComparison.Ordinal) &&
            string.Equals(storedIssue.ReviewStrategy, normalizedIssue.ReviewStrategy, StringComparison.Ordinal) &&
            string.Equals(storedIssue.ScopeCoverage, normalizedIssue.ScopeCoverage, StringComparison.Ordinal) &&
            string.Equals(storedIssue.CrossScopeAnalysis, normalizedIssue.CrossScopeAnalysis, StringComparison.Ordinal);
    }
}
