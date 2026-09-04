using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Workflows.RuleReview;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Workflows.RuleReview;

[TestClass]
public sealed class MessageBuilderTests
{
    [TestMethod]
    public void CreateReviewMessages_UsesStartTemplateAndTaskScopeAsUserMessage()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);
        StoredTaskItem taskItem = CreateTaskItem();

        List<ChatMessage> messages = builder.CreateReviewMessages(taskItem);

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.RuleReviewStartMessage}{Environment.NewLine}{Environment.NewLine}" +
            $"Task item id:{Environment.NewLine}{taskItem.ProjectPlanTaskItemId}{Environment.NewLine}{Environment.NewLine}" +
            $"Scope entry files:{Environment.NewLine}{CodeSnifferDogJson.Serialize(taskItem.Files)}",
            messages[0].Text);
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
    public void CreateVerifierMessages_UsesTaskScopeWithoutEmbeddingReviewResults()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);
        StoredTaskItem taskItem = CreateTaskItem();

        List<ChatMessage> messages = builder.CreateVerifierMessages(taskItem);

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}" +
            $"Task item id:{Environment.NewLine}{taskItem.ProjectPlanTaskItemId}{Environment.NewLine}{Environment.NewLine}" +
            $"Scope entry files:{Environment.NewLine}{CodeSnifferDogJson.Serialize(taskItem.Files)}",
            messages[0].Text);
    }

    [TestMethod]
    public void CreateVerifierMessages_WhenTaskItemIsNull_Throws()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);

        Assert.ThrowsExactly<ArgumentNullException>(() => builder.CreateVerifierMessages(null!));
    }

    private static StoredTaskItem CreateTaskItem() =>
        new()
        {
            ProjectPlanTaskItemId = "task-1",
            Files =
            [
                new PlanFile
                {
                    FilePath = "Program.cs",
                    TotalLines = 120,
                },
            ],
        };
}
