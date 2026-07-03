using CodeSnifferDog.Models.RuleFlow;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Models.ReviewStage;

public sealed class TaskItemFlowResult
{
    public required IReadOnlyList<RuleFlowWorkflowResult> FlowResults { get; init; }
}
