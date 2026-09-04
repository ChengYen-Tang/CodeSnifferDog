using CodeSnifferDog.Json;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools.Listing;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Workflows.Common;
using CodeSnifferDog.Workflows.Scan;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Workflows.Scan;

[TestClass]
public sealed class MessageBuilderTests
{
    [TestMethod]
    public void CreateScanMessages_UsesUserRolePrefixAndRepositoryRoot()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);

        List<ChatMessage> messages = builder.CreateScanMessages(@"Z:\RepoA");

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual($"{templates.ScanInputPrefix}{Environment.NewLine}{Environment.NewLine}Z:\\RepoA", messages[0].Text);
    }

    [TestMethod]
    public void CreateMissingSubmissionMessage_UsesPromptTemplateAsUserMessage()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);

        ChatMessage message = builder.CreateMissingSubmissionMessage();

        Assert.AreEqual(ChatRole.User, message.Role);
        Assert.AreEqual(templates.MissingScanSubmissionMessage, message.Text);
    }

    [TestMethod]
    public void CreateVerifierMessages_UsesUserRolePrefixAndSerializedProjectPage()
    {
        MessageTemplates templates = new(new PromptAssetReader());
        MessageBuilder builder = new(templates);
        ProjectPage projectPage = new()
        {
            Items =
            [
            new()
            {
                ScanProjectId = "scan-1",
                ProjectNamePreview = "Core",
                ProjectPathPreview = "CodeSnifferDog",
                ProjectTypePreview = "Library",
                ReasonPreview = "Contains workflow logic.",
            },
            ],
            HasMore = false,
        };

        List<ChatMessage> messages = builder.CreateVerifierMessages(projectPage);

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(projectPage)}",
            messages[0].Text);
    }

    [TestMethod]
    public void CreateMissingVerifierVerdictMessage_UsesFixedUserMessage()
    {
        MessageBuilder builder = new(new MessageTemplates(new PromptAssetReader()));

        ChatMessage message = builder.CreateMissingVerifierVerdictMessage();

        Assert.AreEqual(ChatRole.User, message.Role);
        Assert.AreEqual(WorkflowRetryMessages.MissingVerifierVerdictMessage, message.Text);
    }
}
