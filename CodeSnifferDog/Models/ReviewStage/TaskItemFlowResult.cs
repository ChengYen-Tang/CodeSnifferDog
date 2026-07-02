using CodeSnifferDog.Models.RuleFlow;

namespace CodeSnifferDog.Models.ReviewStage;

public sealed class TaskItemFlowResult
{
    public required IReadOnlyList<RuleFlowWorkflowResult> FlowResults { get; init; }
}
