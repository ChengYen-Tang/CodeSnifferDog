using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.RuleReview;

/// <summary>
/// Exposes prompt assets used by the rule-review workflow.
/// </summary>
/// <param name="promptAssetReader">Prompt reader used to load workflow prompt assets.</param>
public sealed class MessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    public string RuleReviewStartMessage =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.RuleReviewStartMessage);

    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.VerifierInputPrefix);

    public string MissingRuleReviewSubmissionMessage =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.MissingRuleReviewSubmissionMessage);
}
