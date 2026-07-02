using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using FluentResults;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;
using CodeSnifferDog.Modules.ReviewAgentTeam.Scheduling;

namespace CodeSnifferDog.Workflows.ReviewStage;

internal sealed class ReviewStageWorkflow(
    RuleLaneScheduler scheduler,
    Func<StoredProjectPlanTaskItem, IReadOnlyList<ReviewAgentRuleDefinition>, IReadOnlyList<RuleFlowWorkflowResult>, Result<ReviewGroupWorkflowResult>> reviewGroupWorkflowRunner,
    IAgentEventBus? agentEventBus = null)
{
    private readonly RuleLaneScheduler _scheduler = scheduler;
    private readonly Func<StoredProjectPlanTaskItem, IReadOnlyList<ReviewAgentRuleDefinition>, IReadOnlyList<RuleFlowWorkflowResult>, Result<ReviewGroupWorkflowResult>> _reviewGroupWorkflowRunner = reviewGroupWorkflowRunner;
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;

    public async Task<Result<WorkflowResult>> RunAsync(
        string repositoryRootPath,
        RepositoryPreparationWorkflowResult preparationResult,
        IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<WorkflowResult>("Repository root path is required.");

        ArgumentNullException.ThrowIfNull(preparationResult);
        ArgumentNullException.ThrowIfNull(ruleDefinitions);

        repositoryRootPath = repositoryRootPath.Trim();

        if (preparationResult.ProjectPlanResults.Count == 0)
        {
            return Result.Ok(new WorkflowResult
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
            foreach (StoredProjectPlanTaskItem taskItem in projectPlanResult.TaskItems)
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
            return scheduledFlowResults.ToResult<WorkflowResult>();

        List<IError> errors = [];

        for (int projectIndex = 0; projectIndex < scheduledFlowResults.Value.Count; projectIndex++)
        {
            ProjectFlowResult projectFlowResult = scheduledFlowResults.Value[projectIndex];

            for (int taskItemIndex = 0; taskItemIndex < projectFlowResult.TaskItemResults.Count; taskItemIndex++)
            {
                TaskItemFlowResult taskItemFlowResult = projectFlowResult.TaskItemResults[taskItemIndex];
                StoredProjectPlanTaskItem taskItem = preparationResult.ProjectPlanResults[projectIndex].TaskItems[taskItemIndex];
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
            return Result.Fail<WorkflowResult>(errors);

        return Result.Ok(new WorkflowResult
        {
            ProjectResults = projectResults,
        });
    }
}
