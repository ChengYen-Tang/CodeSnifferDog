using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Models.ProjectPlan;

public sealed class ProjectPlanWorkflowResult
{
    public required StoredScanProject ScanProject { get; init; }

    public required IReadOnlyList<StoredProjectPlanTaskItem> TaskItems { get; init; }

    public required ReviewVerdict Verdict { get; init; }

    public required bool ContinuedAfterVerifierRejectionLimit { get; init; }

    public required int PlanAttempts { get; init; }

    public required int VerifierAttempts { get; init; }

    public required int ProjectPlanAgentResetCount { get; init; }
}
