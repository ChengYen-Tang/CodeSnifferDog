using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.Report;

public sealed class RuleReportWorkflowMessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    public string AggregatorInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(RuleReportWorkflowPromptAssetPaths.AggregatorInputPrefix);

    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(RuleReportWorkflowPromptAssetPaths.VerifierInputPrefix);
}
