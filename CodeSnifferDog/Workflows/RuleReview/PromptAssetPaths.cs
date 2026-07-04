namespace CodeSnifferDog.Workflows.RuleReview;

/// <summary>
/// Prompt asset paths used by the rule-review workflow.
/// </summary>
public static class PromptAssetPaths
{
    /// <summary>
    /// Prompt asset that starts the rule-review workflow.
    /// </summary>
    public const string RuleReviewStartMessage = "workflows/rule-review/rule-review-start.md";

    /// <summary>
    /// Prompt asset prefixed to rule-review verifier input.
    /// </summary>
    public const string VerifierInputPrefix = "workflows/rule-review/review-verifier-input-prefix.md";

    /// <summary>
    /// Prompt asset shown when the rule-review agent fails to submit results.
    /// </summary>
    public const string MissingRuleReviewSubmissionMessage = "workflows/rule-review/missing-rule-review-submission.md";
}
