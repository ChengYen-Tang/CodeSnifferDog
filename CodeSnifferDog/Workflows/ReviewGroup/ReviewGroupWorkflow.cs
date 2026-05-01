using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.RuleFlow;
using FluentResults;

namespace CodeSnifferDog.Workflows.ReviewGroup;

internal static class ReviewGroupWorkflow
{
    public static Result<ReviewGroupWorkflowResult> Run(
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
        IReadOnlyList<RuleFlowWorkflowResult> flowResults)
    {
        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(ruleDefinitions);
        ArgumentNullException.ThrowIfNull(flowResults);

        if (ruleDefinitions.Count == 0)
        {
            return flowResults.Count == 0
                ? Result.Ok(CreateResult(taskItem, [], []))
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

        return Result.Ok(CreateResult(taskItem, [.. ruleDefinitions.Select(ruleDefinition => ruleDefinition.RuleKey)], flowResults));
    }

    private static ReviewGroupWorkflowResult CreateResult(
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<string> ruleKeys,
        IReadOnlyList<RuleFlowWorkflowResult> flowResults)
    {
        int approvedCompletionCount = flowResults.Count(result => result.IsApprovedCompletion);

        return new ReviewGroupWorkflowResult
        {
            TaskItem = taskItem,
            RuleKeys = [.. ruleKeys],
            FlowResults = [.. flowResults],
            HasAnyRuleFlows = flowResults.Count > 0,
            AllRuleFlowsFinished = true,
            ApprovedCompletionCount = approvedCompletionCount,
            DegradedCompletionCount = flowResults.Count - approvedCompletionCount,
        };
    }
}
