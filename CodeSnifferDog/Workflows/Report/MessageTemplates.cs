using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.Report;

public sealed class MessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    public string AggregatorInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.AggregatorInputPrefix);

    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.VerifierInputPrefix);
}
