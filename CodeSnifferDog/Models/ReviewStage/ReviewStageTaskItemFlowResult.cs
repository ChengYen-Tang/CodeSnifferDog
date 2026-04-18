using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleFlow;

namespace CodeSnifferDog.Models.ReviewStage;

public sealed class ReviewStageTaskItemFlowResult
{
    public required StoredProjectPlanTaskItem TaskItem { get; init; }

    public required IReadOnlyList<RuleFlowWorkflowResult> FlowResults { get; init; }
}
