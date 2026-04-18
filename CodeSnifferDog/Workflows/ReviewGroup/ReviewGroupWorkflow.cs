using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.RuleFlow;
using FluentResults;

namespace CodeSnifferDog.Workflows.ReviewGroup;

public sealed class ReviewGroupWorkflow
{
    public Result<ReviewGroupWorkflowResult> Run(
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<string> ruleMarkdowns,
        IReadOnlyList<RuleFlowWorkflowResult> flowResults)
    {
        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(ruleMarkdowns);
        ArgumentNullException.ThrowIfNull(flowResults);

        if (ruleMarkdowns.Count == 0)
        {
            return flowResults.Count == 0
                ? Result.Ok(CreateResult(taskItem, [], []))
                : Result.Fail<ReviewGroupWorkflowResult>("Flow results must be empty when no rules exist.");
        }

        if (flowResults.Count != ruleMarkdowns.Count)
            return Result.Fail<ReviewGroupWorkflowResult>("Flow result count must match rule count.");

        for (int i = 0; i < ruleMarkdowns.Count; i++)
        {
            if (!string.Equals(flowResults[i].TaskItem.ProjectPlanTaskItemId, taskItem.ProjectPlanTaskItemId, StringComparison.Ordinal))
            {
                return Result.Fail<ReviewGroupWorkflowResult>(
                    $"Flow result task item does not match the review group task item at index {i}.");
            }

            if (!string.Equals(flowResults[i].RuleMarkdown, ruleMarkdowns[i], StringComparison.Ordinal))
            {
                return Result.Fail<ReviewGroupWorkflowResult>(
                    $"Flow result order does not match rule order at index {i}.");
            }
        }

        return Result.Ok(CreateResult(taskItem, ruleMarkdowns, flowResults));
    }

    private static ReviewGroupWorkflowResult CreateResult(
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<string> ruleMarkdowns,
        IReadOnlyList<RuleFlowWorkflowResult> flowResults)
    {
        int approvedCompletionCount = flowResults.Count(result => result.IsApprovedCompletion);

        return new ReviewGroupWorkflowResult
        {
            TaskItem = taskItem,
            RuleMarkdowns = ruleMarkdowns.ToArray(),
            FlowResults = flowResults.ToArray(),
            HasAnyRuleFlows = flowResults.Count > 0,
            AllRuleFlowsFinished = true,
            ApprovedCompletionCount = approvedCompletionCount,
            DegradedCompletionCount = flowResults.Count - approvedCompletionCount,
        };
    }
}
