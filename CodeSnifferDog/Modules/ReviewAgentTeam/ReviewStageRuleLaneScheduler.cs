using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Modules.Concurrency;
using FluentResults;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

internal sealed class ReviewStageRuleLaneScheduler(
    Func<string, string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> ruleFlowWorkflowRunner,
    IReviewAgentConcurrencyGate concurrencyGate)
{
    private readonly Func<string, string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> _ruleFlowWorkflowRunner = ruleFlowWorkflowRunner;
    private readonly IReviewAgentConcurrencyGate _concurrencyGate = concurrencyGate;

    public async Task<Result<IReadOnlyList<ReviewStageProjectFlowResult>>> RunAsync(
        string repositoryRootPath,
        IReadOnlyList<ProjectPlanWorkflowResult> projectPlanResults,
        IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<IReadOnlyList<ReviewStageProjectFlowResult>>("Repository root path is required.");

        ArgumentNullException.ThrowIfNull(projectPlanResults);
        ArgumentNullException.ThrowIfNull(ruleDefinitions);

        repositoryRootPath = repositoryRootPath.Trim();

        ReviewStageProjectFlowResult[] projectResults = projectPlanResults
            .Select(projectPlanResult => new ReviewStageProjectFlowResult
            {
                ProjectPlanResult = projectPlanResult,
                TaskItemResults = projectPlanResult.TaskItems
                    .Select(taskItem => new ReviewStageTaskItemFlowResult
                    {
                        TaskItem = taskItem,
                        FlowResults = new RuleFlowWorkflowResult[ruleDefinitions.Count],
                    })
                    .ToArray(),
            })
            .ToArray();

        if (ruleDefinitions.Count == 0)
            return Result.Ok<IReadOnlyList<ReviewStageProjectFlowResult>>(projectResults);

        List<IError> errors = [];
        RuleLaneState[] laneStates = ruleDefinitions
            .Select((ruleDefinition, ruleIndex) => new RuleLaneState(ruleIndex, ruleDefinition))
            .ToArray();

        for (int projectIndex = 0; projectIndex < projectResults.Length; projectIndex++)
        {
            ReviewStageProjectFlowResult projectResult = projectResults[projectIndex];

            for (int taskItemIndex = 0; taskItemIndex < projectResult.TaskItemResults.Count; taskItemIndex++)
                foreach (RuleLaneState laneState in laneStates)
                    laneState.Enqueue(new PendingRuleWorkItem(projectIndex, taskItemIndex, projectResult.TaskItemResults[taskItemIndex].TaskItem));
        }

        List<RunningRuleFlow> runningFlows = [];

        while (HasPendingLaneWork(laneStates) || runningFlows.Count > 0)
        {
            bool launchedAnyFlow = false;

            while (TrySelectNextLane(laneStates, out RuleLaneState? laneState))
            {
                if (!_concurrencyGate.TryAcquire(out IAsyncDisposable? lease))
                    break;

                PendingRuleWorkItem workItem = laneState!.Dequeue();
                laneState.IsRunning = true;
                runningFlows.Add(new RunningRuleFlow(
                    laneState,
                    workItem,
                    RunRuleFlowAsync(lease!, repositoryRootPath, laneState.RuleDefinition.RuleKey, laneState.RuleDefinition.RuleMarkdown, workItem.TaskItem, cancellationToken)));
                launchedAnyFlow = true;
            }

            if (runningFlows.Count == 0)
            {
                if (!HasPendingLaneWork(laneStates))
                    break;

                RuleLaneState laneState = SelectNextLaneOrThrow(laneStates);
                await using IAsyncDisposable lease = await _concurrencyGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
                PendingRuleWorkItem workItem = laneState.Dequeue();
                laneState.IsRunning = true;
                RuleFlowExecutionResult executionResult =
                    await RunRuleFlowAsync(lease, repositoryRootPath, laneState.RuleDefinition.RuleKey, laneState.RuleDefinition.RuleMarkdown, workItem.TaskItem, cancellationToken).ConfigureAwait(false);
                ProcessExecutionResult(executionResult, laneState, workItem, projectResults, errors);
                continue;
            }

            if (launchedAnyFlow && !AllLanesBusyOrEmpty(laneStates))
                continue;

            Task<RuleFlowExecutionResult> completedTask =
                await Task.WhenAny(runningFlows.Select(flow => flow.ExecutionTask)).ConfigureAwait(false);
            RunningRuleFlow completedFlow = runningFlows.Single(flow => ReferenceEquals(flow.ExecutionTask, completedTask));
            runningFlows.Remove(completedFlow);
            RuleFlowExecutionResult execution = await completedFlow.ExecutionTask.ConfigureAwait(false);
            ProcessExecutionResult(execution, completedFlow.LaneState, completedFlow.WorkItem, projectResults, errors);
        }

        return errors.Count > 0
            ? Result.Fail<IReadOnlyList<ReviewStageProjectFlowResult>>(errors)
            : Result.Ok<IReadOnlyList<ReviewStageProjectFlowResult>>(projectResults);
    }

    private static bool HasPendingLaneWork(IReadOnlyList<RuleLaneState> laneStates) =>
        laneStates.Any(laneState => laneState.QueueCount > 0);

    private static bool AllLanesBusyOrEmpty(IReadOnlyList<RuleLaneState> laneStates) =>
        laneStates.All(laneState => laneState.IsRunning || laneState.QueueCount == 0);

    private static bool TrySelectNextLane(IReadOnlyList<RuleLaneState> laneStates, out RuleLaneState? laneState)
    {
        laneState = laneStates
            .Where(candidate => !candidate.IsRunning && candidate.QueueCount > 0)
            .OrderByDescending(candidate => candidate.QueueCount)
            .ThenBy(candidate => candidate.RuleIndex)
            .FirstOrDefault();

        return laneState is not null;
    }

    private static RuleLaneState SelectNextLaneOrThrow(IReadOnlyList<RuleLaneState> laneStates) =>
        TrySelectNextLane(laneStates, out RuleLaneState? laneState)
            ? laneState!
            : throw new InvalidOperationException("No executable rule lane was available.");

    private static void ProcessExecutionResult(
        RuleFlowExecutionResult executionResult,
        RuleLaneState laneState,
        PendingRuleWorkItem workItem,
        IReadOnlyList<ReviewStageProjectFlowResult> projectResults,
        List<IError> errors)
    {
        laneState.IsRunning = false;

        if (executionResult.Result.IsFailed)
        {
            errors.AddRange(executionResult.Result.Errors);
            return;
        }

        RuleFlowWorkflowResult[] flowResults =
            (RuleFlowWorkflowResult[])projectResults[workItem.ProjectIndex].TaskItemResults[workItem.TaskItemIndex].FlowResults;
        flowResults[laneState.RuleIndex] = executionResult.Result.Value;
    }

    private async Task<RuleFlowExecutionResult> RunRuleFlowAsync(
        IAsyncDisposable lease,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        CancellationToken cancellationToken)
    {
        try
        {
            return new RuleFlowExecutionResult(await _ruleFlowWorkflowRunner(
                repositoryRootPath,
                ruleKey,
                ruleMarkdown,
                taskItem,
                cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class PendingRuleWorkItem(int projectIndex, int taskItemIndex, StoredProjectPlanTaskItem taskItem)
    {
        public int ProjectIndex { get; } = projectIndex;

        public int TaskItemIndex { get; } = taskItemIndex;

        public StoredProjectPlanTaskItem TaskItem { get; } = taskItem;
    }

    private sealed class RuleLaneState(int ruleIndex, ReviewAgentRuleDefinition ruleDefinition)
    {
        private readonly Queue<PendingRuleWorkItem> _queue = [];

        public int RuleIndex { get; } = ruleIndex;

        public ReviewAgentRuleDefinition RuleDefinition { get; } = ruleDefinition;

        public bool IsRunning { get; set; }

        public int QueueCount => _queue.Count;

        public void Enqueue(PendingRuleWorkItem workItem) => _queue.Enqueue(workItem);

        public PendingRuleWorkItem Dequeue() => _queue.Dequeue();
    }

    private sealed class RunningRuleFlow(
        RuleLaneState laneState,
        PendingRuleWorkItem workItem,
        Task<RuleFlowExecutionResult> executionTask)
    {
        public RuleLaneState LaneState { get; } = laneState;

        public PendingRuleWorkItem WorkItem { get; } = workItem;

        public Task<RuleFlowExecutionResult> ExecutionTask { get; } = executionTask;
    }

    private sealed class RuleFlowExecutionResult(Result<RuleFlowWorkflowResult> result)
    {
        public Result<RuleFlowWorkflowResult> Result { get; } = result;
    }
}
