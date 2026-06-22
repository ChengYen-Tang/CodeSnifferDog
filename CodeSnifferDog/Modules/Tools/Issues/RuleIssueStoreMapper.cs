using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.Issues;

internal static class RuleIssueStoreMapper
{
    public static StoredRuleReviewIssue CreateReviewIssue(NormalizedRuleIssue normalizedIssue, string id) => new()
    {
        RuleReviewIssueId = id,
        IssueType = normalizedIssue.Issue.IssueType,
        Severity = normalizedIssue.Issue.Severity,
        FileOrFunction = normalizedIssue.Issue.FileOrFunction,
        RelevantCodePatternOrExpression = normalizedIssue.Issue.RelevantCodePatternOrExpression,
        WhyThisIsAProblem = normalizedIssue.Issue.WhyThisIsAProblem,
        Confidence = normalizedIssue.Issue.Confidence,
        FollowUpFiles = normalizedIssue.Issue.FollowUpFiles,
        SuggestedFixDirection = normalizedIssue.Issue.SuggestedFixDirection,
        ReviewStrategy = normalizedIssue.Issue.ReviewStrategy,
        ScopeCoverage = normalizedIssue.Issue.ScopeCoverage,
        CrossScopeAnalysis = normalizedIssue.Issue.CrossScopeAnalysis,
    };

    public static StoredRuleReportIssue CreateReportIssue(NormalizedRuleIssue normalizedIssue, string id) => new()
    {
        RuleReportIssueId = id,
        IssueType = normalizedIssue.Issue.IssueType,
        Severity = normalizedIssue.Issue.Severity,
        FileOrFunction = normalizedIssue.Issue.FileOrFunction,
        RelevantCodePatternOrExpression = normalizedIssue.Issue.RelevantCodePatternOrExpression,
        WhyThisIsAProblem = normalizedIssue.Issue.WhyThisIsAProblem,
        Confidence = normalizedIssue.Issue.Confidence,
        FollowUpFiles = normalizedIssue.Issue.FollowUpFiles,
        SuggestedFixDirection = normalizedIssue.Issue.SuggestedFixDirection,
        ReviewStrategy = normalizedIssue.Issue.ReviewStrategy,
        ScopeCoverage = normalizedIssue.Issue.ScopeCoverage,
        CrossScopeAnalysis = normalizedIssue.Issue.CrossScopeAnalysis,
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

    public static bool IsEquivalentToNormalizedIssue(StoredRuleReviewIssue storedIssue, NormalizedRuleIssue normalizedIssue) =>
        string.Equals(storedIssue.IssueType, normalizedIssue.Issue.IssueType, StringComparison.Ordinal) &&
        string.Equals(storedIssue.Severity, normalizedIssue.Issue.Severity, StringComparison.Ordinal) &&
        string.Equals(storedIssue.FileOrFunction, normalizedIssue.Issue.FileOrFunction, StringComparison.Ordinal) &&
        string.Equals(storedIssue.RelevantCodePatternOrExpression, normalizedIssue.Issue.RelevantCodePatternOrExpression, StringComparison.Ordinal) &&
        string.Equals(storedIssue.WhyThisIsAProblem, normalizedIssue.Issue.WhyThisIsAProblem, StringComparison.Ordinal) &&
        string.Equals(storedIssue.Confidence, normalizedIssue.Issue.Confidence, StringComparison.Ordinal) &&
        string.Equals(storedIssue.FollowUpFiles, normalizedIssue.Issue.FollowUpFiles, StringComparison.Ordinal) &&
        string.Equals(storedIssue.SuggestedFixDirection, normalizedIssue.Issue.SuggestedFixDirection, StringComparison.Ordinal) &&
        string.Equals(storedIssue.ReviewStrategy, normalizedIssue.Issue.ReviewStrategy, StringComparison.Ordinal) &&
        string.Equals(storedIssue.ScopeCoverage, normalizedIssue.Issue.ScopeCoverage, StringComparison.Ordinal) &&
        string.Equals(storedIssue.CrossScopeAnalysis, normalizedIssue.Issue.CrossScopeAnalysis, StringComparison.Ordinal);

    public static bool IsEquivalentToNormalizedIssue(StoredRuleReportIssue storedIssue, NormalizedRuleIssue normalizedIssue) =>
        string.Equals(storedIssue.IssueType, normalizedIssue.Issue.IssueType, StringComparison.Ordinal) &&
        string.Equals(storedIssue.Severity, normalizedIssue.Issue.Severity, StringComparison.Ordinal) &&
        string.Equals(storedIssue.FileOrFunction, normalizedIssue.Issue.FileOrFunction, StringComparison.Ordinal) &&
        string.Equals(storedIssue.RelevantCodePatternOrExpression, normalizedIssue.Issue.RelevantCodePatternOrExpression, StringComparison.Ordinal) &&
        string.Equals(storedIssue.WhyThisIsAProblem, normalizedIssue.Issue.WhyThisIsAProblem, StringComparison.Ordinal) &&
        string.Equals(storedIssue.Confidence, normalizedIssue.Issue.Confidence, StringComparison.Ordinal) &&
        string.Equals(storedIssue.FollowUpFiles, normalizedIssue.Issue.FollowUpFiles, StringComparison.Ordinal) &&
        string.Equals(storedIssue.SuggestedFixDirection, normalizedIssue.Issue.SuggestedFixDirection, StringComparison.Ordinal) &&
        string.Equals(storedIssue.ReviewStrategy, normalizedIssue.Issue.ReviewStrategy, StringComparison.Ordinal) &&
        string.Equals(storedIssue.ScopeCoverage, normalizedIssue.Issue.ScopeCoverage, StringComparison.Ordinal) &&
        string.Equals(storedIssue.CrossScopeAnalysis, normalizedIssue.Issue.CrossScopeAnalysis, StringComparison.Ordinal);
}
