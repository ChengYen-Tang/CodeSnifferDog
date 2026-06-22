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
        NormalizedRuleIssue issue = RuleIssueNormalizer.NormalizeToContract(CreateIssue("High"));

        StoredRuleReviewIssue storedIssue = RuleIssueStoreMapper.CreateReviewIssue(issue, "review-id");

        Assert.AreEqual("review-id", storedIssue.RuleReviewIssueId);
        Assert.AreEqual(issue.Issue.IssueType, storedIssue.IssueType);
        Assert.AreEqual(issue.Issue.Severity, storedIssue.Severity);
        Assert.AreEqual(issue.Issue.FileOrFunction, storedIssue.FileOrFunction);
        Assert.AreEqual(issue.Issue.RelevantCodePatternOrExpression, storedIssue.RelevantCodePatternOrExpression);
        Assert.AreEqual(issue.Issue.WhyThisIsAProblem, storedIssue.WhyThisIsAProblem);
        Assert.AreEqual(issue.Issue.Confidence, storedIssue.Confidence);
        Assert.AreEqual(issue.Issue.FollowUpFiles, storedIssue.FollowUpFiles);
        Assert.AreEqual(issue.Issue.SuggestedFixDirection, storedIssue.SuggestedFixDirection);
        Assert.AreEqual(issue.Issue.ScopeCoverage, storedIssue.ScopeCoverage);
        Assert.AreEqual(issue.Issue.CrossScopeAnalysis, storedIssue.CrossScopeAnalysis);
        Assert.AreEqual(issue.Issue.ReviewStrategy, storedIssue.ReviewStrategy);
    }

    [TestMethod]
    public void CreateReportIssue_PreservesIdAndFields()
    {
        NormalizedRuleIssue issue = RuleIssueNormalizer.NormalizeToContract(CreateIssue("High"));

        StoredRuleReportIssue storedIssue = RuleIssueStoreMapper.CreateReportIssue(issue, "report-id");

        Assert.AreEqual("report-id", storedIssue.RuleReportIssueId);
        Assert.AreEqual(issue.Issue.IssueType, storedIssue.IssueType);
        Assert.AreEqual(issue.Issue.Severity, storedIssue.Severity);
        Assert.AreEqual(issue.Issue.FileOrFunction, storedIssue.FileOrFunction);
    }

    [TestMethod]
    public void Clone_CreatesIndependentReportIssue()
    {
        StoredRuleReportIssue original = RuleIssueStoreMapper.CreateReportIssue(
            RuleIssueNormalizer.NormalizeToContract(CreateIssue("High")),
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
        NormalizedRuleIssue normalizedIssue = RuleIssueNormalizer.NormalizeToContract(CreateIssue("High"));
        StoredRuleReviewIssue storedIssue = RuleIssueStoreMapper.CreateReviewIssue(normalizedIssue, "review-id");
        NormalizedRuleIssue differentlyCasedIssue = RuleIssueNormalizer.NormalizeToContract(CreateIssue("Medium"));

        Assert.IsTrue(RuleIssueStoreMapper.IsEquivalentToNormalizedIssue(storedIssue, normalizedIssue));
        Assert.IsFalse(RuleIssueStoreMapper.IsEquivalentToNormalizedIssue(storedIssue, differentlyCasedIssue));
    }

    [TestMethod]
    public void IsEquivalentToNormalizedIssue_ForReportIssueUsesExactNormalizedFields()
    {
        NormalizedRuleIssue normalizedIssue = RuleIssueNormalizer.NormalizeToContract(CreateIssue("High"));
        StoredRuleReportIssue storedIssue = RuleIssueStoreMapper.CreateReportIssue(normalizedIssue, "report-id");
        NormalizedRuleIssue equivalentIssue = RuleIssueNormalizer.NormalizeToContract(CreateIssue(" high "));

        Assert.IsTrue(RuleIssueStoreMapper.IsEquivalentToNormalizedIssue(storedIssue, equivalentIssue));
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
