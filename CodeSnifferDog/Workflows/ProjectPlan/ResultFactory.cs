using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Workflows.ProjectPlan;

internal static class ResultFactory
{
    public static ProjectPlanWorkflowResult Create(
        StoredScanProject scanProject,
        IReadOnlyList<StoredProjectPlanTaskItem> taskItems,
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
