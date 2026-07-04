using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Workflows.ProjectPlan;

/// <summary>
/// Creates project-plan workflow results from workflow execution state.
/// </summary>
internal static class ResultFactory
{
    /// <summary>
    /// Creates one project-plan workflow result.
    /// </summary>
    /// <param name="scanProject">Scanned project that was planned.</param>
    /// <param name="taskItems">Task items produced by the planner.</param>
    /// <param name="verdict">Latest verifier verdict.</param>
    /// <param name="planAttempts">Number of planner attempts performed.</param>
    /// <param name="verifierAttempts">Number of verifier attempts performed.</param>
    /// <param name="projectPlanAgentResetCount">Number of planner resets triggered after missing submissions.</param>
    /// <param name="continuedAfterVerifierRejectionLimit">Whether the result was accepted after reaching the verifier rejection limit.</param>
    /// <returns>The composed project-plan workflow result.</returns>
    public static WorkflowResult Create(
        StoredScanProject scanProject,
        IReadOnlyList<StoredTaskItem> taskItems,
        ReviewVerdict verdict,
        int planAttempts,
        int verifierAttempts,
        int projectPlanAgentResetCount,
        bool continuedAfterVerifierRejectionLimit) => new()
        {
            ScanProject = scanProject,
            TaskItems = taskItems,
            Verdict = verdict,
            ContinuedAfterVerifierRejectionLimit = continuedAfterVerifierRejectionLimit,
            PlanAttempts = planAttempts,
            VerifierAttempts = verifierAttempts,
            ProjectPlanAgentResetCount = projectPlanAgentResetCount,
        };
}
