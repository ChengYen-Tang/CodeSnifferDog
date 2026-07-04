using CodeSnifferDog.Models.RuleFlow;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Models.ReviewStage;

/// <summary>
/// Holds the rule-flow results produced for one task item.
/// </summary>
public sealed class TaskItemFlowResult
{
    /// <summary>
    /// Gets the rule-flow results for the task item.
    /// </summary>
    public required IReadOnlyList<RuleFlowWorkflowResult> FlowResults { get; init; }
}
