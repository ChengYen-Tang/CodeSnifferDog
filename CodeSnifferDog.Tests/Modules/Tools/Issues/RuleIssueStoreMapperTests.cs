using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Tests.Modules.Tools.Issues;

[TestClass]
public sealed class RuleIssueStoreMapperTests
{
    [TestMethod]
    public void CreateReviewIssue_PreservesIdAndFields()
    {
        RuleReviewIssue issue = RuleIssueNormalizer.Normalize(CreateIssue("High"));

        StoredRuleReviewIssue storedIssue = RuleIssueStoreMapper.CreateReviewIssue(issue, "review-id");

        Assert.AreEqual("review-id", storedIssue.RuleReviewIssueId);
        Assert.AreEqual(issue.IssueType, storedIssue.IssueType);
        Assert.AreEqual(issue.Severity, storedIssue.Severity);
        Assert.AreEqual(issue.FileOrFunction, storedIssue.FileOrFunction);
        Assert.AreEqual(issue.RelevantCodePatternOrExpression, storedIssue.RelevantCodePatternOrExpression);
        Assert.AreEqual(issue.WhyThisIsAProblem, storedIssue.WhyThisIsAProblem);
        Assert.AreEqual(issue.Confidence, storedIssue.Confidence);
        Assert.AreEqual(issue.FollowUpFiles, storedIssue.FollowUpFiles);
        Assert.AreEqual(issue.SuggestedFixDirection, storedIssue.SuggestedFixDirection);
        Assert.AreEqual(issue.ScopeCoverage, storedIssue.ScopeCoverage);
        Assert.AreEqual(issue.CrossScopeAnalysis, storedIssue.CrossScopeAnalysis);
        Assert.AreEqual(issue.ReviewStrategy, storedIssue.ReviewStrategy);
    }

    [TestMethod]
    public void CreateReportIssue_PreservesIdAndFields()
    {
        RuleReviewIssue issue = RuleIssueNormalizer.Normalize(CreateIssue("High"));

        StoredRuleReportIssue storedIssue = RuleIssueStoreMapper.CreateReportIssue(issue, "report-id");

        Assert.AreEqual("report-id", storedIssue.RuleReportIssueId);
        Assert.AreEqual(issue.IssueType, storedIssue.IssueType);
        Assert.AreEqual(issue.Severity, storedIssue.Severity);
        Assert.AreEqual(issue.FileOrFunction, storedIssue.FileOrFunction);
    }

    [TestMethod]
    public void Clone_CreatesIndependentReportIssue()
    {
        StoredRuleReportIssue original = RuleIssueStoreMapper.CreateReportIssue(
            RuleIssueNormalizer.Normalize(CreateIssue("High")),
            "report-id");

        StoredRuleReportIssue clone = RuleIssueStoreMapper.Clone(original);
        StoredRuleReportIssue changedClone = new()
        {
            RuleReportIssueId = clone.RuleReportIssueId,
            IssueType = clone.IssueType,
            Severity = clone.Severity,
            FileOrFunction = "Other.cs",
            RelevantCodePatternOrExpression = clone.RelevantCodePatternOrExpression,
            WhyThisIsAProblem = clone.WhyThisIsAProblem,
            Confidence = clone.Confidence,
            FollowUpFiles = clone.FollowUpFiles,
            SuggestedFixDirection = clone.SuggestedFixDirection,
            ScopeCoverage = clone.ScopeCoverage,
            CrossScopeAnalysis = clone.CrossScopeAnalysis,
            ReviewStrategy = clone.ReviewStrategy,
        };

        Assert.AreEqual("Program.cs", original.FileOrFunction);
        Assert.AreEqual("Other.cs", changedClone.FileOrFunction);
    }

    [TestMethod]
    public void IsEquivalentToNormalizedIssue_UsesExactNormalizedFields()
    {
        RuleReviewIssue normalizedIssue = RuleIssueNormalizer.Normalize(CreateIssue("High"));
        StoredRuleReviewIssue storedIssue = RuleIssueStoreMapper.CreateReviewIssue(normalizedIssue, "review-id");
        RuleReviewIssue differentlyCasedIssue = RuleIssueNormalizer.Normalize(CreateIssue("Medium"));

        Assert.IsTrue(RuleIssueStoreMapper.IsEquivalentToNormalizedIssue(storedIssue, normalizedIssue));
        Assert.IsFalse(RuleIssueStoreMapper.IsEquivalentToNormalizedIssue(storedIssue, differentlyCasedIssue));
    }

    [TestMethod]
    public void IsEquivalentToIssue_UsesNormalizedFieldSemantics()
    {
        RuleReviewIssue normalizedIssue = RuleIssueNormalizer.Normalize(CreateIssue("High"));
        StoredRuleReportIssue storedIssue = RuleIssueStoreMapper.CreateReportIssue(normalizedIssue, "report-id");
        RuleReviewIssue unnormalizedIssue = CreateIssue(" high ");

        Assert.IsTrue(RuleIssueStoreMapper.IsEquivalentToIssue(storedIssue, unnormalizedIssue));
    }

    private static RuleReviewIssue CreateIssue(string severity) => new()
    {
        IssueType = " Performance ",
        Severity = severity,
        FileOrFunction = " Program.cs ",
        RelevantCodePatternOrExpression = " call ",
        WhyThisIsAProblem = " problem ",
        Confidence = " High ",
        FollowUpFiles = " Program.cs ",
        SuggestedFixDirection = " fix ",
        ScopeCoverage = " scope ",
        CrossScopeAnalysis = " cross ",
        ReviewStrategy = " strategy ",
    };
}
