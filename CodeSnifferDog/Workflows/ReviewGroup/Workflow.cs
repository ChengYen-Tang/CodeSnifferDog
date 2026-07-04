using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.RuleFlow;
using FluentResults;
using ReviewGroupWorkflowResult = CodeSnifferDog.Models.ReviewGroup.WorkflowResult;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Workflows.ReviewGroup;

/// <summary>
/// Validates ordered rule-flow results for one task item and packages them as one review-group result.
/// </summary>
internal static class Workflow
{
    /// <summary>
    /// Validates that rule-flow results align with the reviewed task item and rule order.
    /// </summary>
    /// <param name="taskItem">Task item that owns the rule flows.</param>
    /// <param name="ruleDefinitions">Rule definitions expected for the task item.</param>
    /// <param name="flowResults">Rule-flow results produced for the task item.</param>
    /// <returns>The validated review-group result.</returns>
    public static Result<ReviewGroupWorkflowResult> Run(
        StoredTaskItem taskItem,
        IReadOnlyList<RuleDefinition> ruleDefinitions,
        IReadOnlyList<RuleFlowWorkflowResult> flowResults)
    {
        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(ruleDefinitions);
        ArgumentNullException.ThrowIfNull(flowResults);

        if (ruleDefinitions.Count == 0)
        {
            return flowResults.Count == 0
                ? Result.Ok(CreateResult(taskItem, []))
                : Result.Fail<ReviewGroupWorkflowResult>("Flow results must be empty when no rules exist.");
        }

        if (flowResults.Count != ruleDefinitions.Count)
            return Result.Fail<ReviewGroupWorkflowResult>("Flow result count must match rule count.");

        for (int i = 0; i < ruleDefinitions.Count; i++)
        {
            if (!string.Equals(flowResults[i].TaskItem.ProjectPlanTaskItemId, taskItem.ProjectPlanTaskItemId, StringComparison.Ordinal))
            {
                return Result.Fail<ReviewGroupWorkflowResult>(
                    $"Flow result task item does not match the review group task item at index {i}.");
            }

            if (!string.Equals(flowResults[i].RuleKey, ruleDefinitions[i].RuleKey, StringComparison.Ordinal))
            {
                return Result.Fail<ReviewGroupWorkflowResult>(
                    $"Flow result order does not match rule order at index {i}.");
            }
        }

        return Result.Ok(CreateResult(taskItem, flowResults));
    }

    /// <summary>
    /// Creates one review-group result from the validated flow results.
    /// </summary>
    /// <param name="taskItem">Task item that owns the rule flows.</param>
    /// <param name="flowResults">Validated rule-flow results.</param>
    /// <returns>The composed review-group result.</returns>
    private static ReviewGroupWorkflowResult CreateResult(
        StoredTaskItem taskItem,
        IReadOnlyList<RuleFlowWorkflowResult> flowResults)
        => new()
        {
            TaskItem = taskItem,
            FlowResults = [.. flowResults],
        };
}
