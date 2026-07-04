using CodeSnifferDog.Json;
using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Agents.Common;

/// <summary>
/// Reads prompt assets and renders prompt templates for agent factories.
/// </summary>
/// <param name="promptAssetReader">Optional prompt reader used to load prompt assets.</param>
/// <param name="promptTemplateRenderer">Optional template renderer used to substitute placeholders.</param>
internal sealed class AgentPromptRenderer(
    PromptAssetReader? promptAssetReader = null,
    PromptTemplateRenderer? promptTemplateRenderer = null)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader ?? new();
    private readonly PromptTemplateRenderer _promptTemplateRenderer = promptTemplateRenderer ?? new();

    /// <summary>
    /// Reads one required prompt asset by relative path.
    /// </summary>
    /// <param name="relativePromptPath">Prompt asset path relative to the prompt root.</param>
    /// <returns>The loaded prompt text.</returns>
    public string ReadRequiredPrompt(string relativePromptPath) =>
        _promptAssetReader.ReadRequiredPrompt(relativePromptPath);

    /// <summary>
    /// Renders one prompt template with string placeholders.
    /// </summary>
    /// <param name="promptTemplate">Prompt template text.</param>
    /// <param name="placeholders">Placeholder values keyed by template token name.</param>
    /// <returns>The rendered prompt text.</returns>
    public string Render(
        string promptTemplate,
        IReadOnlyDictionary<string, string> placeholders) =>
        _promptTemplateRenderer.Render(promptTemplate, placeholders);

    /// <summary>
    /// Serializes one value as JSON for prompt injection.
    /// </summary>
    /// <param name="value">Value to serialize.</param>
    /// <returns>The serialized JSON string.</returns>
    public static string JsonValue(object value) =>
        CodeSnifferDogJson.Serialize(value);
}
