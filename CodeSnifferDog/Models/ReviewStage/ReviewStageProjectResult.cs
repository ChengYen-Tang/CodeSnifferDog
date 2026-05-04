using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewGroup;
namespace CodeSnifferDog.Models.ReviewStage;

public sealed class ReviewStageProjectResult
{
    public required ProjectPlanWorkflowResult ProjectPlanResult { get; init; }

    public required IReadOnlyList<ReviewGroupWorkflowResult> ReviewGroupResults { get; init; }
}
