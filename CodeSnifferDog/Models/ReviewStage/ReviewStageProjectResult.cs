using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Models.ReviewStage;

public sealed class ReviewStageProjectResult
{
    public required StoredScanProject ScanProject { get; init; }

    public required ProjectPlanWorkflowResult ProjectPlanResult { get; init; }

    public required IReadOnlyList<ReviewGroupWorkflowResult> ReviewGroupResults { get; init; }
}
