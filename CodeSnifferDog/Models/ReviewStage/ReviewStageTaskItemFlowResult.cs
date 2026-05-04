using CodeSnifferDog.Models.RuleFlow;

namespace CodeSnifferDog.Models.ReviewStage;

public sealed class ReviewStageTaskItemFlowResult
{
    public required IReadOnlyList<RuleFlowWorkflowResult> FlowResults { get; init; }
}
