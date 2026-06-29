using CodeSnifferDog.Json;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Workflows.Scan;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Workflows.Scan;

[TestClass]
public sealed class ScanWorkflowMessageBuilderTests
{
    [TestMethod]
    public void CreateScanMessages_UsesUserRolePrefixAndRepositoryRoot()
    {
        ScanWorkflowMessageTemplates templates = new(new PromptAssetReader());
        ScanWorkflowMessageBuilder builder = new(templates);

        List<ChatMessage> messages = builder.CreateScanMessages(@"Z:\RepoA");

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual($"{templates.ScanInputPrefix}{Environment.NewLine}{Environment.NewLine}Z:\\RepoA", messages[0].Text);
    }

    [TestMethod]
    public void CreateMissingSubmissionMessage_UsesPromptTemplateAsUserMessage()
    {
        ScanWorkflowMessageTemplates templates = new(new PromptAssetReader());
        ScanWorkflowMessageBuilder builder = new(templates);

        ChatMessage message = builder.CreateMissingSubmissionMessage();

        Assert.AreEqual(ChatRole.User, message.Role);
        Assert.AreEqual(templates.MissingScanSubmissionMessage, message.Text);
    }

    [TestMethod]
    public void CreateVerifierMessages_UsesUserRolePrefixAndSerializedProjects()
    {
        ScanWorkflowMessageTemplates templates = new(new PromptAssetReader());
        ScanWorkflowMessageBuilder builder = new(templates);
        StoredScanProject[] projects =
        [
            new()
            {
                ScanProjectId = "scan-1",
                ProjectName = "Core",
                ProjectPath = "CodeSnifferDog",
                ProjectType = "Library",
                Reason = "Contains workflow logic.",
            },
        ];

        List<ChatMessage> messages = builder.CreateVerifierMessages(projects);

        Assert.HasCount(1, messages);
        Assert.AreEqual(ChatRole.User, messages[0].Role);
        Assert.AreEqual(
            $"{templates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(projects)}",
            messages[0].Text);
    }
}
