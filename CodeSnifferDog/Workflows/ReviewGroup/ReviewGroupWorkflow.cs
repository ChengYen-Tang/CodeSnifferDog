using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.RuleFlow;
using FluentResults;

namespace CodeSnifferDog.Workflows.ReviewGroup;

public sealed class ReviewGroupWorkflow(
    Func<string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> ruleFlowWorkflowRunner,
    ReviewGroupWorkflowOptions? options = null)
{
    private readonly Func<string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> _ruleFlowWorkflowRunner = ruleFlowWorkflowRunner;
    private readonly ReviewGroupWorkflowOptions _options = options ?? new();

    public async Task<Result<ReviewGroupWorkflowResult>> RunAsync(
        string repositoryRootPath,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<string> ruleMarkdowns,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<ReviewGroupWorkflowResult>("Repository root path is required.");

        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(ruleMarkdowns);

        if (_options.MaxConcurrentRuleFlows <= 0)
            return Result.Fail<ReviewGroupWorkflowResult>("MaxConcurrentRuleFlows must be greater than zero.");

        repositoryRootPath = repositoryRootPath.Trim();

        if (ruleMarkdowns.Count == 0)
        {
            return Result.Ok(new ReviewGroupWorkflowResult
            {
                TaskItem = taskItem,
                RuleMarkdowns = [],
                FlowResults = [],
                HasAnyRuleFlows = false,
                AllRuleFlowsFinished = true,
                ApprovedCompletionCount = 0,
                DegradedCompletionCount = 0,
            });
        }

        RuleFlowWorkflowResult[] orderedResults = new RuleFlowWorkflowResult[ruleMarkdowns.Count];
        List<IError> errors = [];
        using SemaphoreSlim semaphore = new(_options.MaxConcurrentRuleFlows);

        Task[] tasks = ruleMarkdowns
            .Select((ruleMarkdown, index) =>
                RunRuleFlowAsync(ruleMarkdown, index, orderedResults, errors, semaphore, repositoryRootPath, taskItem, cancellationToken))
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (errors.Count > 0)
            return Result.Fail<ReviewGroupWorkflowResult>(errors);

        int approvedCompletionCount = orderedResults.Count(result => result.IsApprovedCompletion);

        return Result.Ok(new ReviewGroupWorkflowResult
        {
            TaskItem = taskItem,
            RuleMarkdowns = ruleMarkdowns.ToArray(),
            FlowResults = orderedResults,
            HasAnyRuleFlows = true,
            AllRuleFlowsFinished = true,
            ApprovedCompletionCount = approvedCompletionCount,
            DegradedCompletionCount = orderedResults.Length - approvedCompletionCount,
        });
    }

    private async Task RunRuleFlowAsync(
        string ruleMarkdown,
        int index,
        RuleFlowWorkflowResult[] orderedResults,
        List<IError> errors,
        SemaphoreSlim semaphore,
        string repositoryRootPath,
        StoredProjectPlanTaskItem taskItem,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Result<RuleFlowWorkflowResult> result =
                await _ruleFlowWorkflowRunner(repositoryRootPath, ruleMarkdown, taskItem, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                orderedResults[index] = result.Value;
                return;
            }

            lock (errors)
            {
                foreach (IError error in result.Errors)
                    errors.Add(error);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
}
