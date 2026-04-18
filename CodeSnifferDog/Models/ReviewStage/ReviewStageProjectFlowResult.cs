using CodeSnifferDog.Models.ProjectPlan;

namespace CodeSnifferDog.Models.ReviewStage;

public sealed class ReviewStageProjectFlowResult
{
    public required ProjectPlanWorkflowResult ProjectPlanResult { get; init; }

    public required IReadOnlyList<ReviewStageTaskItemFlowResult> TaskItemResults { get; init; }
}
