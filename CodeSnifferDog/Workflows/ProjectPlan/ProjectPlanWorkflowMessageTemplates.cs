using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.ProjectPlan;

public sealed class ProjectPlanWorkflowMessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    public string PlanInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(ProjectPlanWorkflowPromptAssetPaths.PlanInputPrefix);

    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(ProjectPlanWorkflowPromptAssetPaths.VerifierInputPrefix);

    public string MissingProjectPlanSubmissionMessage =>
        _promptAssetReader.ReadRequiredPrompt(ProjectPlanWorkflowPromptAssetPaths.MissingProjectPlanSubmissionMessage);
}
