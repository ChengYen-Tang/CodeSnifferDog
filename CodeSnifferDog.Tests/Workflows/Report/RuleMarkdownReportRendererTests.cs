using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Workflows.Report;

namespace CodeSnifferDog.Tests.Workflows.Report;

[TestClass]
public sealed class RuleMarkdownReportRendererTests
{
    [TestMethod]
    public void Render_SortsIssuesBySeverityDescending()
    {
        StoredRuleReportIssue[] issues =
        [
            CreateIssue("Low", "LowFile.cs", "Low issue"),
            CreateIssue("High", "HighFile.cs", "High issue"),
            CreateIssue("Medium", "MediumFile.cs", "Medium issue"),
        ];

        string markdown = RuleMarkdownReportRenderer.Render("performance", issues);

        int highIndex = markdown.IndexOf("High issue", StringComparison.Ordinal);
        int mediumIndex = markdown.IndexOf("Medium issue", StringComparison.Ordinal);
        int lowIndex = markdown.IndexOf("Low issue", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, highIndex);
        Assert.IsGreaterThanOrEqualTo(0, mediumIndex);
        Assert.IsGreaterThanOrEqualTo(0, lowIndex);
        Assert.IsLessThan(mediumIndex, highIndex);
        Assert.IsLessThan(lowIndex, mediumIndex);
        StringAssert.Contains(markdown, "- High: 1");
        StringAssert.Contains(markdown, "- Medium: 1");
        StringAssert.Contains(markdown, "- Low: 1");
        StringAssert.Contains(markdown, "- Severity: High");
        StringAssert.Contains(markdown, "- Severity: Medium");
        StringAssert.Contains(markdown, "- Severity: Low");
    }

    private static StoredRuleReportIssue CreateIssue(string severity, string fileOrFunction, string issueType) =>
        new()
        {
            RuleReportIssueId = Guid.NewGuid().ToString("N"),
            IssueType = issueType,
            Severity = severity,
            FileOrFunction = fileOrFunction,
            RelevantCodePatternOrExpression = "Pattern",
            WhyThisIsAProblem = "Problem",
            Confidence = "High",
            FollowUpFiles = fileOrFunction,
            SuggestedFixDirection = "Fix direction",
            ReviewStrategy = "Reviewed path",
            ScopeCoverage = $"Inspected {fileOrFunction}.",
            CrossScopeAnalysis = "No extra tracing required.",
        };
}
