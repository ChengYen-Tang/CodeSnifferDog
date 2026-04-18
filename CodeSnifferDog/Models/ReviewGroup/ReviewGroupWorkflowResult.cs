using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleFlow;

namespace CodeSnifferDog.Models.ReviewGroup;

public sealed class ReviewGroupWorkflowResult
{
    public required StoredProjectPlanTaskItem TaskItem { get; init; }

    public required IReadOnlyList<string> RuleMarkdowns { get; init; }

    public required IReadOnlyList<RuleFlowWorkflowResult> FlowResults { get; init; }

    public required bool HasAnyRuleFlows { get; init; }

    public required bool AllRuleFlowsFinished { get; init; }

    public required int ApprovedCompletionCount { get; init; }

    public required int DegradedCompletionCount { get; init; }
}
