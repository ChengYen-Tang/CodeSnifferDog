using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.RuleReview;

public sealed class RuleReviewWorkflowMessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    public string RuleReviewStartMessage =>
        _promptAssetReader.ReadRequiredPrompt(RuleReviewWorkflowPromptAssetPaths.RuleReviewStartMessage);

    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(RuleReviewWorkflowPromptAssetPaths.VerifierInputPrefix);

    public string MissingRuleReviewSubmissionMessage =>
        _promptAssetReader.ReadRequiredPrompt(RuleReviewWorkflowPromptAssetPaths.MissingRuleReviewSubmissionMessage);
}
