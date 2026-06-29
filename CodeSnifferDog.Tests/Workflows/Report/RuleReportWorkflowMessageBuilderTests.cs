using CodeSnifferDog.Json;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Workflows.Report;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Workflows.Report;

[TestClass]
public sealed class RuleReportWorkflowMessageBuilderTests
{
    [TestMethod]
    public void CreateAggregatorMessages_UsesUserRolePrefixAndSerializedCurrentFlowIssues()
    {
        RuleReportWorkflowMessageTemplates templates = new(new PromptAssetReader());
        RuleReportWorkflowMessageBuilder builder = new(templates);
        StoredRuleReviewIssue[] issues = [CreateReviewIssue()];

        List<ChatMessage> messages = builder.CreateAggregatorMessages(issues);

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.AggregatorInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(issues)}",
            messages[0].Text);
    }

    [TestMethod]
    public void CreateVerifierMessages_UsesUserRolePrefixAndSerializedDiff()
    {
        RuleReportWorkflowMessageTemplates templates = new(new PromptAssetReader());
        RuleReportWorkflowMessageBuilder builder = new(templates);
        RuleReportDiff diff = new()
        {
            CreatedIssues = [CreateReportIssue()],
            UpdatedIssues = [],
            DeletedIssues = [],
        };

        List<ChatMessage> messages = builder.CreateVerifierMessages(diff);

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(diff)}",
            messages[0].Text);
    }

    private static StoredRuleReviewIssue CreateReviewIssue() =>
        new()
        {
            RuleReviewIssueId = "review-issue-1",
            IssueType = "Performance",
            Severity = "High",
            FileOrFunction = "Program.cs",
            RelevantCodePatternOrExpression = "Repeated synchronous call",
            WhyThisIsAProblem = "This blocks the hot path.",
            Confidence = "High",
            FollowUpFiles = "Program.cs",
            SuggestedFixDirection = "Use a cached async path.",
            ReviewStrategy = "Reviewed the hot path first.",
            ScopeCoverage = "Inspected Program.cs.",
            CrossScopeAnalysis = "No cross-scope inspection was required.",
        };

    private static StoredRuleReportIssue CreateReportIssue() =>
        new()
        {
            RuleReportIssueId = "report-issue-1",
            IssueType = "Performance",
            Severity = "High",
            FileOrFunction = "Program.cs",
            RelevantCodePatternOrExpression = "Repeated synchronous call",
            WhyThisIsAProblem = "This blocks the hot path.",
            Confidence = "High",
            FollowUpFiles = "Program.cs",
            SuggestedFixDirection = "Use a cached async path.",
            ReviewStrategy = "Reviewed the hot path first.",
            ScopeCoverage = "Inspected Program.cs.",
            CrossScopeAnalysis = "No cross-scope inspection was required.",
        };
}
