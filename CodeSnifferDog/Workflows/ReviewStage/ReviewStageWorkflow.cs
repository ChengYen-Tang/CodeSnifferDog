using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using FluentResults;

namespace CodeSnifferDog.Workflows.ReviewStage;

internal sealed class ReviewStageWorkflow(
    ReviewStageRuleLaneScheduler scheduler,
    Func<StoredProjectPlanTaskItem, IReadOnlyList<ReviewAgentRuleDefinition>, IReadOnlyList<RuleFlowWorkflowResult>, Result<ReviewGroupWorkflowResult>> reviewGroupWorkflowRunner,
    IAgentStatusEventPublisher? agentStatusEventPublisher = null)
{
    private readonly ReviewStageRuleLaneScheduler _scheduler = scheduler;
    private readonly Func<StoredProjectPlanTaskItem, IReadOnlyList<ReviewAgentRuleDefinition>, IReadOnlyList<RuleFlowWorkflowResult>, Result<ReviewGroupWorkflowResult>> _reviewGroupWorkflowRunner = reviewGroupWorkflowRunner;
    private readonly IAgentStatusEventPublisher _agentStatusEventPublisher = agentStatusEventPublisher ?? NoOpAgentStatusEventPublisher.Instance;

    public async Task<Result<ReviewStageWorkflowResult>> RunAsync(
        string repositoryRootPath,
        RepositoryPreparationWorkflowResult preparationResult,
        IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
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
                    [.. preparationResult.ProjectPlanResults.Select(projectPlanResult => new ReviewStageProjectResult
                    {
                        ProjectPlanResult = projectPlanResult,
                        ReviewGroupResults = [],
                    })],
            });
        }

        ReviewStageProjectResult[] projectResults =
            [.. preparationResult.ProjectPlanResults.Select(projectPlanResult => new ReviewStageProjectResult
            {
                ProjectPlanResult = projectPlanResult,
                ReviewGroupResults = new ReviewGroupWorkflowResult[projectPlanResult.TaskItems.Count],
            })];

        foreach (ProjectPlanWorkflowResult projectPlanResult in preparationResult.ProjectPlanResults)
        {
            foreach (StoredProjectPlanTaskItem taskItem in projectPlanResult.TaskItems)
            {
                await _agentStatusEventPublisher.PublishAsync(new AgentGroupCreatedEvent
                {
                    GroupKey = AgentStatusCatalog.CreateReviewTaskGroupKey(taskItem),
                    DisplayName = AgentStatusCatalog.CreateReviewTaskGroupDisplayName(taskItem),
                    OccurredAtUtc = DateTimeOffset.UtcNow,
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        Result<IReadOnlyList<ReviewStageProjectFlowResult>> scheduledFlowResults =
            await _scheduler.RunAsync(repositoryRootPath, preparationResult.ProjectPlanResults, ruleDefinitions, cancellationToken).ConfigureAwait(false);

        if (scheduledFlowResults.IsFailed)
            return scheduledFlowResults.ToResult<ReviewStageWorkflowResult>();

        List<IError> errors = [];

        for (int projectIndex = 0; projectIndex < scheduledFlowResults.Value.Count; projectIndex++)
        {
            ReviewStageProjectFlowResult projectFlowResult = scheduledFlowResults.Value[projectIndex];

            for (int taskItemIndex = 0; taskItemIndex < projectFlowResult.TaskItemResults.Count; taskItemIndex++)
            {
                ReviewStageTaskItemFlowResult taskItemFlowResult = projectFlowResult.TaskItemResults[taskItemIndex];
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
            return Result.Fail<ReviewStageWorkflowResult>(errors);

        return Result.Ok(new ReviewStageWorkflowResult
        {
            ProjectResults = projectResults,
        });
    }
}
