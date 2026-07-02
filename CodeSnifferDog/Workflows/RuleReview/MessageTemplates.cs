using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.RuleReview;

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
