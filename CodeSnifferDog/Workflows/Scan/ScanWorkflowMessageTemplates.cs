using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.Scan;

public sealed class ScanWorkflowMessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    public string ScanInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(ScanWorkflowPromptAssetPaths.ScanInputPrefix);

    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(ScanWorkflowPromptAssetPaths.VerifierInputPrefix);

    public string MissingScanSubmissionMessage =>
        _promptAssetReader.ReadRequiredPrompt(ScanWorkflowPromptAssetPaths.MissingScanSubmissionMessage);
}
