namespace CodeSnifferDog.Agents.RuleReview;

/// <summary>
/// Prompt asset paths used by rule-review agents and rule-review context compaction.
/// </summary>
public static class PromptAssetPaths
{
    /// <summary>
    /// Prompt asset for the rule-review agent.
    /// </summary>
    public const string RuleReviewAgentPrompt = "rule-review-agent.md";

    /// <summary>
    /// Prompt asset for the rule-review verifier agent.
    /// </summary>
    public const string ReviewVerifierAgentPrompt = "review-verifier-agent.md";

    /// <summary>
    /// Prompt asset used when summarizing rule-review context during compaction.
    /// </summary>
    public const string RuleReviewSummaryPrompt = "compaction/rule-review-summary.md";
}
