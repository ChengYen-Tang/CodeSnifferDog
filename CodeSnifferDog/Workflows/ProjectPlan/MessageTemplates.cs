using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.ProjectPlan;

/// <summary>
/// Exposes prompt assets used by the project-plan workflow.
/// </summary>
/// <param name="promptAssetReader">Prompt reader used to load workflow prompt assets.</param>
public sealed class MessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    /// <summary>
    /// Gets the prompt prefix injected before project-plan agent input.
    /// </summary>
    public string PlanInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.PlanInputPrefix);

    /// <summary>
    /// Gets the prompt prefix injected before project-plan verifier input.
    /// </summary>
    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.VerifierInputPrefix);

    /// <summary>
    /// Gets the fallback message shown when the project-plan agent does not submit a result.
    /// </summary>
    public string MissingProjectPlanSubmissionMessage =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.MissingProjectPlanSubmissionMessage);
}
