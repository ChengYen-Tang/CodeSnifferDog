using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;
using ReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;

namespace CodeSnifferDog.Modules.Tools.Issues;

/// <summary>
/// Maps normalized issues to stored issue shapes used by review and report tool stores.
/// </summary>
internal static class RuleIssueStoreMapper
{
    /// <summary>
    /// Creates a stored review issue from a normalized issue.
    /// </summary>
    /// <param name="normalizedIssue">Normalized issue payload.</param>
    /// <param name="id">Generated issue identifier.</param>
    /// <returns>The stored review issue.</returns>
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

    /// <summary>
    /// Creates a stored report issue from a normalized issue.
    /// </summary>
    /// <param name="normalizedIssue">Normalized issue payload.</param>
    /// <param name="id">Generated issue identifier.</param>
    /// <returns>The stored report issue.</returns>
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

    /// <summary>
    /// Clones a stored review issue.
    /// </summary>
    /// <param name="issue">Stored review issue to clone.</param>
    /// <returns>The cloned issue.</returns>
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

    /// <summary>
    /// Clones a stored report issue.
    /// </summary>
    /// <param name="issue">Stored report issue to clone.</param>
    /// <returns>The cloned issue.</returns>
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

    /// <summary>
    /// Determines whether a stored review issue is equivalent to a normalized issue.
    /// </summary>
    /// <param name="storedIssue">Stored review issue to compare.</param>
    /// <param name="normalizedIssue">Normalized issue to compare against.</param>
    /// <returns><see langword="true"/> when the issues are equivalent; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Determines whether a stored report issue is equivalent to a normalized issue.
    /// </summary>
    /// <param name="storedIssue">Stored report issue to compare.</param>
    /// <param name="normalizedIssue">Normalized issue to compare against.</param>
    /// <returns><see langword="true"/> when the issues are equivalent; otherwise, <see langword="false"/>.</returns>
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
