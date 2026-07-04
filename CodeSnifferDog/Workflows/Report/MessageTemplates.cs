using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.Report;

/// <summary>
/// Exposes prompt assets used by the report workflow.
/// </summary>
/// <param name="promptAssetReader">Prompt reader used to load workflow prompt assets.</param>
public sealed class MessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    /// <summary>
    /// Gets the prompt prefix injected before report aggregator input.
    /// </summary>
    public string AggregatorInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.AggregatorInputPrefix);

    /// <summary>
    /// Gets the prompt prefix injected before report verifier input.
    /// </summary>
    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.VerifierInputPrefix);
}
