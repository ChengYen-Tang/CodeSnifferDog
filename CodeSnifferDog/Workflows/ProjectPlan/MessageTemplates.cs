using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.ProjectPlan;

/// <summary>
/// Exposes prompt assets used by the project-plan workflow.
/// </summary>
/// <param name="promptAssetReader">Prompt reader used to load workflow prompt assets.</param>
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
