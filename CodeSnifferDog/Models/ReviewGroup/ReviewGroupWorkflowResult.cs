using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleFlow;

namespace CodeSnifferDog.Models.ReviewGroup;

public sealed class ReviewGroupWorkflowResult
{
    public required StoredProjectPlanTaskItem TaskItem { get; init; }

    public required IReadOnlyList<RuleFlowWorkflowResult> FlowResults { get; init; }
}
