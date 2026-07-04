using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using FluentResults;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;
using CodeSnifferDog.Modules.ReviewAgentTeam.Scheduling;
using StoredTaskItem = CodeSnifferDog.Models.ProjectPlan.StoredTaskItem;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;
using PreparationWorkflowResult = CodeSnifferDog.Models.Preparation.WorkflowResult;
using ReviewGroupWorkflowResult = CodeSnifferDog.Models.ReviewGroup.WorkflowResult;
using ReviewStageWorkflowResult = CodeSnifferDog.Models.ReviewStage.WorkflowResult;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Workflows.ReviewStage;

/// <summary>
/// Orchestrates review execution for all planned task items, then groups the scheduled rule-flow results back into project results.
/// </summary>
/// <param name="scheduler">Scheduler that runs rule flows across planned projects and task items.</param>
/// <param name="reviewGroupWorkflowRunner">Creates one review-group result from ordered rule-flow results.</param>
/// <param name="agentEventBus">Optional event bus used to publish review-task group creation events.</param>
internal sealed class Workflow(
    RuleLaneScheduler scheduler,
    Func<StoredTaskItem, IReadOnlyList<RuleDefinition>, IReadOnlyList<RuleFlowWorkflowResult>, Result<ReviewGroupWorkflowResult>> reviewGroupWorkflowRunner,
    IAgentEventBus? agentEventBus = null)
{
    private readonly RuleLaneScheduler _scheduler = scheduler;
    private readonly Func<StoredTaskItem, IReadOnlyList<RuleDefinition>, IReadOnlyList<RuleFlowWorkflowResult>, Result<ReviewGroupWorkflowResult>> _reviewGroupWorkflowRunner = reviewGroupWorkflowRunner;
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;

    /// <summary>
    /// Runs the review stage for all planned projects in one preparation result.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path that contains the reviewed code.</param>
    /// <param name="preparationResult">Preparation result that supplies planned projects and task items.</param>
    /// <param name="ruleDefinitions">Rule definitions that should run for every task item.</param>
    /// <param name="cancellationToken">Cancels the workflow.</param>
    /// <returns>The review-stage workflow result.</returns>
    public async Task<Result<ReviewStageWorkflowResult>> RunAsync(
        string repositoryRootPath,
        PreparationWorkflowResult preparationResult,
        IReadOnlyList<RuleDefinition> ruleDefinitions,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<ReviewStageWorkflowResult>("Repository root path is required.");

        ArgumentNullException.ThrowIfNull(preparationResult);
        ArgumentNullException.ThrowIfNull(ruleDefinitions);

        repositoryRootPath = repositoryRootPath.Trim();

        if (preparationResult.ProjectPlanResults.Count == 0)
        {
            return Result.Ok(new ReviewStageWorkflowResult
            {
                ProjectResults =
                    [.. preparationResult.ProjectPlanResults.Select(projectPlanResult => new ProjectResult
                    {
                        ProjectPlanResult = projectPlanResult,
                        ReviewGroupResults = [],
                    })],
            });
        }

        ProjectResult[] projectResults =
            [.. preparationResult.ProjectPlanResults.Select(projectPlanResult => new ProjectResult
            {
                ProjectPlanResult = projectPlanResult,
                ReviewGroupResults = new ReviewGroupWorkflowResult[projectPlanResult.TaskItems.Count],
            })];

        int reviewNumber = 1;

        foreach (ProjectPlanWorkflowResult projectPlanResult in preparationResult.ProjectPlanResults)
        {
            foreach (StoredTaskItem taskItem in projectPlanResult.TaskItems)
            {
                await _agentEventBus.PublishGroupCreatedAsync(
                    AgentStatusCatalog.CreateReviewTaskGroupKey(taskItem),
                    AgentStatusCatalog.CreateReviewTaskGroupDisplayName(reviewNumber),
                    cancellationToken).ConfigureAwait(false);
                reviewNumber++;
            }
        }

        Result<IReadOnlyList<ProjectFlowResult>> scheduledFlowResults =
            await _scheduler.RunAsync(repositoryRootPath, preparationResult.ProjectPlanResults, ruleDefinitions, cancellationToken).ConfigureAwait(false);

        if (scheduledFlowResults.IsFailed)
            return scheduledFlowResults.ToResult<ReviewStageWorkflowResult>();

        List<IError> errors = [];

        for (int projectIndex = 0; projectIndex < scheduledFlowResults.Value.Count; projectIndex++)
        {
            ProjectFlowResult projectFlowResult = scheduledFlowResults.Value[projectIndex];

            for (int taskItemIndex = 0; taskItemIndex < projectFlowResult.TaskItemResults.Count; taskItemIndex++)
            {
                TaskItemFlowResult taskItemFlowResult = projectFlowResult.TaskItemResults[taskItemIndex];
                StoredTaskItem taskItem = preparationResult.ProjectPlanResults[projectIndex].TaskItems[taskItemIndex];
                Result<ReviewGroupWorkflowResult> reviewGroupResult =
                    _reviewGroupWorkflowRunner(taskItem, ruleDefinitions, taskItemFlowResult.FlowResults);

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
            ProjectResults = projectResults,
        });
    }
}
