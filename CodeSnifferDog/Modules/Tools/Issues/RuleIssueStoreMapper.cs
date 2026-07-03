using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;
using ReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;

namespace CodeSnifferDog.Modules.Tools.Issues;

internal static class RuleIssueStoreMapper
{
    public static ReviewStoredIssue CreateReviewIssue(NormalizedRuleIssue normalizedIssue, string id) => new()
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

    public static ReportStoredIssue CreateReportIssue(NormalizedRuleIssue normalizedIssue, string id) => new()
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

    public static ReviewStoredIssue Clone(ReviewStoredIssue issue) => new()
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

    public static ReportStoredIssue Clone(ReportStoredIssue issue) => new()
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

    public static bool IsEquivalentToNormalizedIssue(ReviewStoredIssue storedIssue, NormalizedRuleIssue normalizedIssue) =>
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

    public static bool IsEquivalentToNormalizedIssue(ReportStoredIssue storedIssue, NormalizedRuleIssue normalizedIssue) =>
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
