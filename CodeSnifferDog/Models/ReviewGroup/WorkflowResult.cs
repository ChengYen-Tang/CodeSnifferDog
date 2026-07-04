using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleFlow;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Models.ReviewGroup;

/// <summary>
/// Holds the rule-flow results produced for one project-plan task item.
/// </summary>
public sealed class WorkflowResult
{
    /// <summary>
    /// Gets the task item whose rules were reviewed.
    /// </summary>
    public required StoredTaskItem TaskItem { get; init; }

    /// <summary>
    /// Gets the rule-flow results produced for the task item.
    /// </summary>
    public required IReadOnlyList<RuleFlowWorkflowResult> FlowResults { get; init; }
}
