using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools.Listing;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Workflows.ProjectPlan;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Workflows.ProjectPlan;

[TestClass]
public sealed class MessageBuilderTests
{
    [TestMethod]
    public void CreatePlanMessages_UsesUserRolePrefixAndSerializedScanProject()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);
        StoredScanProject scanProject = CreateScanProject();

        List<ChatMessage> messages = builder.CreatePlanMessages(scanProject);

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.PlanInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(scanProject)}",
            messages[0].Text);
    }

    [TestMethod]
    public void CreateMissingSubmissionMessage_UsesPromptTemplateAsUserMessage()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);

        ChatMessage message = builder.CreateMissingSubmissionMessage();

        Assert.AreEqual(ChatRole.User, message.Role);
        Assert.AreEqual(templates.MissingProjectPlanSubmissionMessage, message.Text);
    }

    [TestMethod]
    public void CreateVerifierMessages_UsesUserRolePrefixAndSerializedTaskItemPage()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);
        TaskItemPage taskItemPage = new()
        {
            Items =
            [
                new TaskItemListItem
                {
                    ProjectPlanTaskItemId = "task-1",
                    FileCount = 1,
                    TotalLines = 120,
                    FirstFilePathPreview = "Program.cs",
                },
            ],
            HasMore = false,
        };

        List<ChatMessage> messages = builder.CreateVerifierMessages(taskItemPage);

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(taskItemPage)}",
            messages[0].Text);
    }

    private static StoredScanProject CreateScanProject() =>
        new()
        {
            ScanProjectId = "scan-1",
            ProjectName = "Core",
            ProjectPath = "CodeSnifferDog",
            ProjectType = "Library",
            Reason = "Contains workflow logic.",
        };

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
