using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Workflows.Report;

namespace CodeSnifferDog.Tests.Workflows.Report;

[TestClass]
public sealed class MarkdownRendererTests
{
    [TestMethod]
    public void Render_SortsIssuesBySeverityDescending()
    {
        StoredIssue[] issues =
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
        Assert.Contains("- High: 1", markdown);
        Assert.Contains("- Medium: 1", markdown);
        Assert.Contains("- Low: 1", markdown);
        Assert.Contains("- Severity: High", markdown);
        Assert.Contains("- Severity: Medium", markdown);
        Assert.Contains("- Severity: Low", markdown);
    }

    private static StoredIssue CreateIssue(string severity, string fileOrFunction, string issueType) =>
        new()
        {
            RuleReportIssueId = Guid.CreateVersion7().ToString("N"),
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
