using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Agents.Scan;
using CodeSnifferDog.Json;
using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Tests.Agents;

[TestClass]
public sealed class AgentPromptRendererTests
{
    [TestMethod]
    public void ReadRequiredPrompt_LoadsPromptAsset()
    {
        AgentPromptRenderer renderer = new();

        string prompt = renderer.ReadRequiredPrompt(ScanAgentPromptAssets.ScanAgentPrompt);

        Assert.IsFalse(string.IsNullOrWhiteSpace(prompt));
    }

    [TestMethod]
    public void Render_ReplacesPlaceholders()
    {
        AgentPromptRenderer renderer = new();

        string rendered = renderer.Render(
            "Repository: {{RepositoryRootPath}}",
            new Dictionary<string, string>
            {
                ["RepositoryRootPath"] = "Z:\\repo",
            });

        Assert.AreEqual("Repository: Z:\\repo", rendered);
    }

    [TestMethod]
    public void JsonValue_UsesCodeSnifferDogJsonSerialization()
    {
        var value = new { Name = "Project", Count = 2 };

        Assert.AreEqual(CodeSnifferDogJson.Serialize(value), AgentPromptRenderer.JsonValue(value));
    }

    [TestMethod]
    public void MissingPrompt_PreservesPromptAssetReaderException()
    {
        AgentPromptRenderer renderer = new();

        FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(
            () => renderer.ReadRequiredPrompt("missing/prompt.md"));

        Assert.AreEqual("Prompt asset was not found.", exception.Message.Split(Environment.NewLine)[0]);
    }

    [TestMethod]
    public void EmptyTemplate_PreservesTemplateRendererException()
    {
        AgentPromptRenderer renderer = new();

        Assert.ThrowsExactly<ArgumentException>(
            () => renderer.Render("", new Dictionary<string, string>()));
    }
}
