namespace CodeSnifferDog.Agents.ProjectPlan;

/// <summary>
/// Prompt asset paths used by project-plan agents and project-plan context compaction.
/// </summary>
public static class PromptAssetPaths
{
    /// <summary>
    /// Prompt asset for the project-plan agent.
    /// </summary>
    public const string ProjectPlanAgentPrompt = "project-plan-agent.md";

    /// <summary>
    /// Prompt asset for the project-plan verifier agent.
    /// </summary>
    public const string ProjectVerifierAgentPrompt = "project-verifier-agent.md";

    /// <summary>
    /// Prompt asset used when summarizing project-plan context during compaction.
    /// </summary>
    public const string ProjectPlanSummaryPrompt = "compaction/project-plan-summary.md";
}
