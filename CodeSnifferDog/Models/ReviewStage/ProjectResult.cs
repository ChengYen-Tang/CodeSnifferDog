using CodeSnifferDog.Models.ReviewGroup;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;
using ReviewGroupWorkflowResult = CodeSnifferDog.Models.ReviewGroup.WorkflowResult;

namespace CodeSnifferDog.Models.ReviewStage;

public sealed class ProjectResult
{
    public required ProjectPlanWorkflowResult ProjectPlanResult { get; init; }

    public required IReadOnlyList<ReviewGroupWorkflowResult> ReviewGroupResults { get; init; }
}
