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
    public void NormalizeToContract_ReturnsNormalizedWrapper()
    {
        NormalizedRuleIssue contract = RuleIssueNormalizer.NormalizeToContract(CreateIssue(" high "));

        Assert.AreEqual("Performance", contract.Issue.IssueType);
        Assert.AreEqual(RuleReviewSeverity.High, contract.Issue.Severity);
        Assert.AreEqual("Program.cs", contract.Issue.FileOrFunction);
    }

    [TestMethod]
    public void Create_UsesSameNormalization_ForToolArguments()
    {
        NormalizedRuleIssue fromIssue = RuleIssueNormalizer.NormalizeToContract(CreateIssue(" high "));
        NormalizedRuleIssue fromArguments = RuleIssueNormalizer.CreateContract(
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

        Assert.AreEqual(fromIssue.Issue.IssueType, fromArguments.Issue.IssueType);
        Assert.AreEqual(fromIssue.Issue.Severity, fromArguments.Issue.Severity);
        Assert.AreEqual(fromIssue.Issue.FileOrFunction, fromArguments.Issue.FileOrFunction);
        Assert.AreEqual(fromIssue.Issue.RelevantCodePatternOrExpression, fromArguments.Issue.RelevantCodePatternOrExpression);
        Assert.AreEqual(fromIssue.Issue.WhyThisIsAProblem, fromArguments.Issue.WhyThisIsAProblem);
        Assert.AreEqual(fromIssue.Issue.Confidence, fromArguments.Issue.Confidence);
        Assert.AreEqual(fromIssue.Issue.FollowUpFiles, fromArguments.Issue.FollowUpFiles);
        Assert.AreEqual(fromIssue.Issue.SuggestedFixDirection, fromArguments.Issue.SuggestedFixDirection);
        Assert.AreEqual(fromIssue.Issue.ScopeCoverage, fromArguments.Issue.ScopeCoverage);
        Assert.AreEqual(fromIssue.Issue.CrossScopeAnalysis, fromArguments.Issue.CrossScopeAnalysis);
        Assert.AreEqual(fromIssue.Issue.ReviewStrategy, fromArguments.Issue.ReviewStrategy);
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
