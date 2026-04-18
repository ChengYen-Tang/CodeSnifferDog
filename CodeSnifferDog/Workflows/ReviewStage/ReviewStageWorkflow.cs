using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.ReviewStage;
using FluentResults;

namespace CodeSnifferDog.Workflows.ReviewStage;

public sealed class ReviewStageWorkflow(
    Func<string, StoredProjectPlanTaskItem, IReadOnlyList<string>, CancellationToken, Task<Result<ReviewGroupWorkflowResult>>> reviewGroupWorkflowRunner,
    ReviewStageWorkflowOptions? options = null)
{
    private readonly Func<string, StoredProjectPlanTaskItem, IReadOnlyList<string>, CancellationToken, Task<Result<ReviewGroupWorkflowResult>>> _reviewGroupWorkflowRunner = reviewGroupWorkflowRunner;
    private readonly ReviewStageWorkflowOptions _options = options ?? new();

    public async Task<Result<ReviewStageWorkflowResult>> RunAsync(
        string repositoryRootPath,
        RepositoryPreparationWorkflowResult preparationResult,
        IReadOnlyList<string> ruleMarkdowns,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<ReviewStageWorkflowResult>("Repository root path is required.");

        ArgumentNullException.ThrowIfNull(preparationResult);
        ArgumentNullException.ThrowIfNull(ruleMarkdowns);

        if (_options.MaxConcurrentReviewGroups <= 0)
            return Result.Fail<ReviewStageWorkflowResult>("MaxConcurrentReviewGroups must be greater than zero.");

        repositoryRootPath = repositoryRootPath.Trim();

        ReviewStageProjectResult[] projectResults = new ReviewStageProjectResult[preparationResult.ProjectPlanResults.Count];

        for (int i = 0; i < preparationResult.ProjectPlanResults.Count; i++)
        {
            ProjectPlanWorkflowResult projectPlanResult = preparationResult.ProjectPlanResults[i];
            projectResults[i] = new ReviewStageProjectResult
            {
                ScanProject = projectPlanResult.ScanProject,
                ProjectPlanResult = projectPlanResult,
                ReviewGroupResults = new ReviewGroupWorkflowResult[projectPlanResult.TaskItems.Count],
            };
        }

        if (!preparationResult.ShouldEnterRuleReview)
        {
            return Result.Ok(new ReviewStageWorkflowResult
            {
                PreparationResult = preparationResult,
                ProjectResults = projectResults,
                RuleMarkdowns = ruleMarkdowns.ToArray(),
                HasAnyReviewGroups = false,
                AllReviewGroupsFinished = false,
            });
        }

        List<IError> errors = [];
        using SemaphoreSlim semaphore = new(_options.MaxConcurrentReviewGroups);

        Task[] tasks = preparationResult.ProjectPlanResults
            .SelectMany((projectPlanResult, projectIndex) => projectPlanResult.TaskItems.Select((taskItem, taskItemIndex) =>
                RunReviewGroupAsync(
                    repositoryRootPath,
                    taskItem,
                    ruleMarkdowns,
                    projectIndex,
                    taskItemIndex,
                    projectResults,
                    errors,
                    semaphore,
                    cancellationToken)))
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (errors.Count > 0)
            return Result.Fail<ReviewStageWorkflowResult>(errors);

        return Result.Ok(new ReviewStageWorkflowResult
        {
            PreparationResult = preparationResult,
            ProjectResults = projectResults,
            RuleMarkdowns = ruleMarkdowns.ToArray(),
            HasAnyReviewGroups = projectResults.Any(project => project.ReviewGroupResults.Count > 0),
            AllReviewGroupsFinished = true,
        });
    }

    private async Task RunReviewGroupAsync(
        string repositoryRootPath,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<string> ruleMarkdowns,
        int projectIndex,
        int taskItemIndex,
        ReviewStageProjectResult[] projectResults,
        List<IError> errors,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Result<ReviewGroupWorkflowResult> result =
                await _reviewGroupWorkflowRunner(repositoryRootPath, taskItem, ruleMarkdowns, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                ((ReviewGroupWorkflowResult[])projectResults[projectIndex].ReviewGroupResults)[taskItemIndex] = result.Value;
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
