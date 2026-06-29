using CodeSnifferDog.Json;
using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Agents.Common;

internal sealed class AgentPromptRenderer(
    PromptAssetReader? promptAssetReader = null,
    PromptTemplateRenderer? promptTemplateRenderer = null)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader ?? new();
    private readonly PromptTemplateRenderer _promptTemplateRenderer = promptTemplateRenderer ?? new();

    public string ReadRequiredPrompt(string relativePromptPath) =>
        _promptAssetReader.ReadRequiredPrompt(relativePromptPath);

    public string Render(
        string promptTemplate,
        IReadOnlyDictionary<string, string> placeholders) =>
        _promptTemplateRenderer.Render(promptTemplate, placeholders);

    public static string JsonValue(object value) =>
        CodeSnifferDogJson.Serialize(value);
}
