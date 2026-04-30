using CodeSnifferDog.Models.Report;

namespace CodeSnifferDog.Workflows.Report;

public static class RuleMarkdownReportRenderer
{
    public static string Render(string ruleName, IReadOnlyList<StoredRuleReportIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);
        ArgumentNullException.ThrowIfNull(issues);

        StringWriter writer = new();
        writer.WriteLine($"# {ruleName}-report.md");
        writer.WriteLine();
        writer.WriteLine("## Summary");
        writer.WriteLine();

        if (issues.Count == 0)
        {
            writer.WriteLine("No issues were reported for this rule in the latest completed analysis.");
            return writer.ToString().TrimEnd();
        }

        writer.WriteLine($"{issues.Count} issue(s) were reported for this rule in the latest completed analysis.");

        for (int index = 0; index < issues.Count; index++)
        {
            StoredRuleReportIssue issue = issues[index];
            writer.WriteLine();
            writer.WriteLine($"## Finding {index + 1}");
            writer.WriteLine();
            writer.WriteLine($"- Issue type: {issue.IssueType}");
            writer.WriteLine($"- File / function: {issue.FileOrFunction}");
            writer.WriteLine($"- Relevant code pattern / expression: {issue.RelevantCodePatternOrExpression}");
            writer.WriteLine($"- Why this is a problem: {issue.WhyThisIsAProblem}");
            writer.WriteLine($"- Confidence: {issue.Confidence}");
            writer.WriteLine($"- Follow-up file(s): {issue.FollowUpFiles}");
            writer.WriteLine($"- Suggested fix direction: {issue.SuggestedFixDirection}");
            writer.WriteLine();
            writer.WriteLine("### Review Strategy");
            writer.WriteLine();
            writer.WriteLine(issue.ReviewStrategy);
            writer.WriteLine();
            writer.WriteLine("### Scope Coverage");
            writer.WriteLine();
            writer.WriteLine(issue.ScopeCoverage);
            writer.WriteLine();
            writer.WriteLine("### Cross-Scope Analysis");
            writer.WriteLine();
            writer.WriteLine(issue.CrossScopeAnalysis);
        }

        return writer.ToString().TrimEnd();
    }
}
