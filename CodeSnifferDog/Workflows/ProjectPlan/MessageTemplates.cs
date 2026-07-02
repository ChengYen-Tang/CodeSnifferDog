using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.ProjectPlan;

public sealed class MessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    public string PlanInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.PlanInputPrefix);

    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.VerifierInputPrefix);

    public string MissingProjectPlanSubmissionMessage =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.MissingProjectPlanSubmissionMessage);
}
