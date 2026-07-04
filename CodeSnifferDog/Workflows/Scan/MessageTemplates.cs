using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.Scan;

/// <summary>
/// Exposes prompt assets used by the scan workflow.
/// </summary>
/// <param name="promptAssetReader">Prompt reader used to load workflow prompt assets.</param>
public sealed class MessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    /// <summary>
    /// Gets the prompt prefix injected before scan-agent input.
    /// </summary>
    public string ScanInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.ScanInputPrefix);

    /// <summary>
    /// Gets the prompt prefix injected before scan verifier input.
    /// </summary>
    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.VerifierInputPrefix);

    /// <summary>
    /// Gets the fallback message shown when the scan agent does not submit a result.
    /// </summary>
    public string MissingScanSubmissionMessage =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.MissingScanSubmissionMessage);
}
