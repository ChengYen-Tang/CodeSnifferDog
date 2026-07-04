namespace CodeSnifferDog.Workflows.ProjectPlan;

/// <summary>
/// Prompt asset paths used by the project-plan workflow.
/// </summary>
public static class PromptAssetPaths
{
    /// <summary>
    /// Prompt asset prefixed to project-plan agent input.
    /// </summary>
    public const string PlanInputPrefix = "workflows/project-plan/project-plan-input-prefix.md";

    /// <summary>
    /// Prompt asset prefixed to project-plan verifier input.
    /// </summary>
    public const string VerifierInputPrefix = "workflows/project-plan/project-verifier-input-prefix.md";

    /// <summary>
    /// Prompt asset shown when the project-plan agent fails to submit results.
    /// </summary>
    public const string MissingProjectPlanSubmissionMessage = "workflows/project-plan/missing-project-plan-submission.md";
}
