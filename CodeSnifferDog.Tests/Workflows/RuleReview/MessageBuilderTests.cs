using CodeSnifferDog.Json;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Workflows.RuleReview;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Workflows.RuleReview;

[TestClass]
public sealed class MessageBuilderTests
{
    [TestMethod]
    public void CreateReviewMessages_UsesStartTemplateAsUserMessage()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);

        List<ChatMessage> messages = builder.CreateReviewMessages();

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(templates.RuleReviewStartMessage, messages[0].Text);
    }

    [TestMethod]
    public void CreateMissingSubmissionMessage_UsesPromptTemplateAsUserMessage()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);

        ChatMessage message = builder.CreateMissingSubmissionMessage();

        Assert.AreEqual(ChatRole.User, message.Role);
        Assert.AreEqual(templates.MissingRuleReviewSubmissionMessage, message.Text);
    }

    [TestMethod]
    public void CreateVerifierMessages_WhenIssuesExist_UsesSerializedIssuesPayload()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);
        StoredRuleReviewIssue[] issues = [CreateIssue()];

        List<ChatMessage> messages = builder.CreateVerifierMessages(issues, CreateNoIssueConclusion());

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(issues)}",
            messages[0].Text);
    }

    [TestMethod]
    public void CreateVerifierMessages_WhenNoIssuesExist_UsesSerializedNoIssueConclusionPayload()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);
        NoIssueConclusion noIssueConclusion = CreateNoIssueConclusion();

        List<ChatMessage> messages = builder.CreateVerifierMessages([], noIssueConclusion);

        Assert.AreEqual(
            $"{templates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(noIssueConclusion)}",
            messages[0].Text);
    }

    [TestMethod]
    public void CreateVerifierMessages_WhenNoReviewResultExists_Throws()
    {
        MessageBuilder builder = new(new MessageTemplates(new PromptAssetReader()));

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.CreateVerifierMessages([], null));
    }

    private static StoredRuleReviewIssue CreateIssue() =>
        new()
        {
            RuleReviewIssueId = "issue-1",
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

    private static NoIssueConclusion CreateNoIssueConclusion() =>
        new()
        {
            ReviewStrategy = "Reviewed the target files.",
            ScopeCoverage = "Covered Program.cs.",
            CrossScopeAnalysis = "No cross-scope inspection was required.",
            WhyNoIssueWasFound = "The rule is satisfied.",
        };
}
