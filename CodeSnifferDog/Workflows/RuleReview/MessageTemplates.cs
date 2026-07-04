using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Workflows.RuleReview;

/// <summary>
/// Exposes prompt assets used by the rule-review workflow.
/// </summary>
/// <param name="promptAssetReader">Prompt reader used to load workflow prompt assets.</param>
public sealed class MessageTemplates(PromptAssetReader promptAssetReader)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;

    /// <summary>
    /// Gets the prompt that starts the rule-review workflow.
    /// </summary>
    public string RuleReviewStartMessage =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.RuleReviewStartMessage);

    /// <summary>
    /// Gets the prompt prefix injected before rule-review verifier input.
    /// </summary>
    public string VerifierInputPrefix =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.VerifierInputPrefix);

    /// <summary>
    /// Gets the fallback message shown when the rule-review agent does not submit a result.
    /// </summary>
    public string MissingRuleReviewSubmissionMessage =>
        _promptAssetReader.ReadRequiredPrompt(PromptAssetPaths.MissingRuleReviewSubmissionMessage);
}
