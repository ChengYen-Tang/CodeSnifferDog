using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.Scan;

/// <summary>
/// Exposes prompt assets used by the scan workflow.
/// </summary>
/// <param name="promptAssetReader">Prompt reader used to load workflow prompt assets.</param>
public sealed class MessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    public string ScanInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.ScanInputPrefix);

    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.VerifierInputPrefix);

    public string MissingScanSubmissionMessage =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.MissingScanSubmissionMessage);
}
