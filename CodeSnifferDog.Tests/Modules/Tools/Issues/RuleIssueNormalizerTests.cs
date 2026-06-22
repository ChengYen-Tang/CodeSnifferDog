using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Issues;

namespace CodeSnifferDog.Tests.Modules.Tools.Issues;

[TestClass]
public sealed class RuleIssueNormalizerTests
{
    [TestMethod]
    public void Normalize_TrimsFieldsAndNormalizesSeverity()
    {
        RuleReviewIssue issue = RuleIssueNormalizer.Normalize(new RuleReviewIssue
        {
            IssueType = " Performance ",
            Severity = " high ",
            FileOrFunction = " Program.cs ",
            RelevantCodePatternOrExpression = " call ",
            WhyThisIsAProblem = " problem ",
            Confidence = " High ",
            FollowUpFiles = " Program.cs ",
            SuggestedFixDirection = " fix ",
            ScopeCoverage = " scope ",
            CrossScopeAnalysis = " cross ",
            ReviewStrategy = " strategy ",
        });

        Assert.AreEqual("Performance", issue.IssueType);
        Assert.AreEqual(RuleReviewSeverity.High, issue.Severity);
        Assert.AreEqual("Program.cs", issue.FileOrFunction);
        Assert.AreEqual("call", issue.RelevantCodePatternOrExpression);
        Assert.AreEqual("problem", issue.WhyThisIsAProblem);
        Assert.AreEqual("High", issue.Confidence);
        Assert.AreEqual("Program.cs", issue.FollowUpFiles);
        Assert.AreEqual("fix", issue.SuggestedFixDirection);
        Assert.AreEqual("scope", issue.ScopeCoverage);
        Assert.AreEqual("cross", issue.CrossScopeAnalysis);
        Assert.AreEqual("strategy", issue.ReviewStrategy);
    }

    [TestMethod]
    public void Create_UsesSameNormalization_ForToolArguments()
    {
        RuleReviewIssue fromIssue = RuleIssueNormalizer.Normalize(CreateIssue(" high "));
        RuleReviewIssue fromArguments = RuleIssueNormalizer.Create(
            " Performance ",
            " high ",
            " Program.cs ",
            " call ",
            " problem ",
            " High ",
            " Program.cs ",
            " fix ",
            " scope ",
            " cross ",
            " strategy ");

        Assert.AreEqual(fromIssue.IssueType, fromArguments.IssueType);
        Assert.AreEqual(fromIssue.Severity, fromArguments.Severity);
        Assert.AreEqual(fromIssue.FileOrFunction, fromArguments.FileOrFunction);
        Assert.AreEqual(fromIssue.RelevantCodePatternOrExpression, fromArguments.RelevantCodePatternOrExpression);
        Assert.AreEqual(fromIssue.WhyThisIsAProblem, fromArguments.WhyThisIsAProblem);
        Assert.AreEqual(fromIssue.Confidence, fromArguments.Confidence);
        Assert.AreEqual(fromIssue.FollowUpFiles, fromArguments.FollowUpFiles);
        Assert.AreEqual(fromIssue.SuggestedFixDirection, fromArguments.SuggestedFixDirection);
        Assert.AreEqual(fromIssue.ScopeCoverage, fromArguments.ScopeCoverage);
        Assert.AreEqual(fromIssue.CrossScopeAnalysis, fromArguments.CrossScopeAnalysis);
        Assert.AreEqual(fromIssue.ReviewStrategy, fromArguments.ReviewStrategy);
    }

    [TestMethod]
    public void Normalize_ThrowsArgumentException_WhenRequiredFieldIsEmpty()
    {
        RuleReviewIssue issue = CreateIssue("High", fileOrFunction: " ");

        Assert.ThrowsExactly<ArgumentException>(() => RuleIssueNormalizer.Normalize(issue));
    }

    [TestMethod]
    public void Normalize_ThrowsArgumentOutOfRangeException_WhenSeverityIsInvalid()
    {
        RuleReviewIssue issue = CreateIssue("Critical");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RuleIssueNormalizer.Normalize(issue));
    }

    private static RuleReviewIssue CreateIssue(string severity, string fileOrFunction = " Program.cs ") => new()
    {
        IssueType = " Performance ",
        Severity = severity,
        FileOrFunction = fileOrFunction,
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
