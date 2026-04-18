using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using FluentResults;

namespace CodeSnifferDog.Workflows.ReviewStage;

internal sealed class ReviewStageWorkflow(
    ReviewStageRuleLaneScheduler scheduler,
    Func<StoredProjectPlanTaskItem, IReadOnlyList<string>, IReadOnlyList<RuleFlowWorkflowResult>, Result<ReviewGroupWorkflowResult>> reviewGroupWorkflowRunner)
{
    private readonly ReviewStageRuleLaneScheduler _scheduler = scheduler;
    private readonly Func<StoredProjectPlanTaskItem, IReadOnlyList<string>, IReadOnlyList<RuleFlowWorkflowResult>, Result<ReviewGroupWorkflowResult>> _reviewGroupWorkflowRunner = reviewGroupWorkflowRunner;

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

        repositoryRootPath = repositoryRootPath.Trim();

        ReviewStageProjectResult[] projectResults = preparationResult.ProjectPlanResults
            .Select(projectPlanResult => new ReviewStageProjectResult
            {
                ScanProject = projectPlanResult.ScanProject,
                ProjectPlanResult = projectPlanResult,
                ReviewGroupResults = new ReviewGroupWorkflowResult[projectPlanResult.TaskItems.Count],
            })
            .ToArray();

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

        Result<IReadOnlyList<ReviewStageProjectFlowResult>> scheduledFlowResults =
            await _scheduler.RunAsync(repositoryRootPath, preparationResult.ProjectPlanResults, ruleMarkdowns, cancellationToken).ConfigureAwait(false);

        if (scheduledFlowResults.IsFailed)
            return scheduledFlowResults.ToResult<ReviewStageWorkflowResult>();

        List<IError> errors = [];

        for (int projectIndex = 0; projectIndex < scheduledFlowResults.Value.Count; projectIndex++)
        {
            ReviewStageProjectFlowResult projectFlowResult = scheduledFlowResults.Value[projectIndex];

            for (int taskItemIndex = 0; taskItemIndex < projectFlowResult.TaskItemResults.Count; taskItemIndex++)
            {
                ReviewStageTaskItemFlowResult taskItemFlowResult = projectFlowResult.TaskItemResults[taskItemIndex];
                Result<ReviewGroupWorkflowResult> reviewGroupResult =
                    _reviewGroupWorkflowRunner(taskItemFlowResult.TaskItem, ruleMarkdowns, taskItemFlowResult.FlowResults);

                if (reviewGroupResult.IsSuccess)
                {
                    ((ReviewGroupWorkflowResult[])projectResults[projectIndex].ReviewGroupResults)[taskItemIndex] = reviewGroupResult.Value;
                    continue;
                }

                errors.AddRange(reviewGroupResult.Errors);
            }
        }

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
}
