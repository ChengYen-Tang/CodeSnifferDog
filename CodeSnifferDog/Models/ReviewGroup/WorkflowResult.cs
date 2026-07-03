using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleFlow;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Models.ReviewGroup;

public sealed class WorkflowResult
{
    public required StoredTaskItem TaskItem { get; init; }

    public required IReadOnlyList<RuleFlowWorkflowResult> FlowResults { get; init; }
}
