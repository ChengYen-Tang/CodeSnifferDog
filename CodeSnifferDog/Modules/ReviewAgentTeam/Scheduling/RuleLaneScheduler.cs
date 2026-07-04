using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Modules.Concurrency;
using FluentResults;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;
using ReviewStageWorkflowResult = CodeSnifferDog.Models.ReviewStage.WorkflowResult;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Modules.ReviewAgentTeam.Scheduling;

/// <summary>
/// Schedules rule-flow executions across rule-specific lanes while respecting the shared agent concurrency gate.
/// </summary>
/// <param name="ruleFlowWorkflowRunner">Delegate that executes one rule flow for one task item.</param>
/// <param name="concurrencyGate">Concurrency gate that limits how many rule flows may run simultaneously.</param>
internal sealed class RuleLaneScheduler(
    Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> ruleFlowWorkflowRunner,
    IReviewAgentConcurrencyGate concurrencyGate)
{
    private readonly Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> _ruleFlowWorkflowRunner = ruleFlowWorkflowRunner;
    private readonly IReviewAgentConcurrencyGate _concurrencyGate = concurrencyGate;

    /// <summary>
    /// Runs every rule flow for every prepared task item and returns results in project/task-item/rule order.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path that the rule-flow runner should analyze.</param>
    /// <param name="projectPlanResults">Prepared project-plan results that provide task items per project.</param>
    /// <param name="ruleDefinitions">Rule definitions to execute for each task item.</param>
    /// <param name="cancellationToken">Cancels scheduling and in-flight rule-flow executions.</param>
    /// <returns>The per-project flow results, or a failed result containing collected rule-flow errors.</returns>
    public async Task<Result<IReadOnlyList<ProjectFlowResult>>> RunAsync(
        string repositoryRootPath,
        IReadOnlyList<ProjectPlanWorkflowResult> projectPlanResults,
        IReadOnlyList<RuleDefinition> ruleDefinitions,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<IReadOnlyList<ProjectFlowResult>>("Repository root path is required.");

        ArgumentNullException.ThrowIfNull(projectPlanResults);
        ArgumentNullException.ThrowIfNull(ruleDefinitions);

        repositoryRootPath = repositoryRootPath.Trim();

        ProjectFlowResult[] projectResults =
            [.. projectPlanResults.Select(projectPlanResult => new ProjectFlowResult
            {
                TaskItemResults =
                    [.. projectPlanResult.TaskItems.Select(taskItem => new TaskItemFlowResult
                    {
                        FlowResults = new RuleFlowWorkflowResult[ruleDefinitions.Count],
                    })],
            })];

        if (ruleDefinitions.Count == 0)
            return Result.Ok<IReadOnlyList<ProjectFlowResult>>(projectResults);

        List<IError> errors = [];
        RuleLaneState[] laneStates =
            [.. ruleDefinitions.Select((ruleDefinition, ruleIndex) => new RuleLaneState(ruleIndex, ruleDefinition))];

        for (int projectIndex = 0; projectIndex < projectResults.Length; projectIndex++)
        {
            ProjectFlowResult projectResult = projectResults[projectIndex];
            ProjectPlanWorkflowResult projectPlanResult = projectPlanResults[projectIndex];

            for (int taskItemIndex = 0; taskItemIndex < projectResult.TaskItemResults.Count; taskItemIndex++)
                foreach (RuleLaneState laneState in laneStates)
                    laneState.Enqueue(new PendingRuleWorkItem(projectIndex, taskItemIndex, projectPlanResult.TaskItems[taskItemIndex]));
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
            ? Result.Fail<IReadOnlyList<ProjectFlowResult>>(errors)
            : Result.Ok<IReadOnlyList<ProjectFlowResult>>(projectResults);
    }

    /// <summary>
    /// Determines whether any lane still has queued work.
    /// </summary>
    /// <param name="laneStates">Lane states to inspect.</param>
    /// <returns><see langword="true" /> when at least one lane still has queued work.</returns>
    private static bool HasPendingLaneWork(IReadOnlyList<RuleLaneState> laneStates) =>
        laneStates.Any(laneState => laneState.QueueCount > 0);

    /// <summary>
    /// Determines whether every lane is either already running or has no queued work left.
    /// </summary>
    /// <param name="laneStates">Lane states to inspect.</param>
    /// <returns><see langword="true" /> when no immediately launchable lane exists.</returns>
    private static bool AllLanesBusyOrEmpty(IReadOnlyList<RuleLaneState> laneStates) =>
        laneStates.All(laneState => laneState.IsRunning || laneState.QueueCount == 0);

    /// <summary>
    /// Selects the next runnable lane, preferring the deepest queue and then the lowest rule index.
    /// </summary>
    /// <param name="laneStates">Lane states to inspect.</param>
    /// <param name="laneState">Selected runnable lane when one exists.</param>
    /// <returns><see langword="true" /> when a runnable lane was found.</returns>
    private static bool TrySelectNextLane(IReadOnlyList<RuleLaneState> laneStates, out RuleLaneState? laneState)
    {
        laneState = laneStates
            .Where(candidate => !candidate.IsRunning && candidate.QueueCount > 0)
            .OrderByDescending(candidate => candidate.QueueCount)
            .ThenBy(candidate => candidate.RuleIndex)
            .FirstOrDefault();

        return laneState is not null;
    }

    /// <summary>
    /// Selects the next runnable lane or throws when no executable lane is available.
    /// </summary>
    /// <param name="laneStates">Lane states to inspect.</param>
    /// <returns>The selected runnable lane.</returns>
    /// <exception cref="InvalidOperationException">No runnable lane exists.</exception>
    private static RuleLaneState SelectNextLaneOrThrow(IReadOnlyList<RuleLaneState> laneStates) =>
        TrySelectNextLane(laneStates, out RuleLaneState? laneState)
            ? laneState!
            : throw new InvalidOperationException("No executable rule lane was available.");

    /// <summary>
    /// Records one completed rule-flow execution back into the projected result grid and error list.
    /// </summary>
    /// <param name="executionResult">Completed rule-flow execution result.</param>
    /// <param name="laneState">Lane that owned the execution.</param>
    /// <param name="workItem">Work item that was executed.</param>
    /// <param name="projectResults">Projected result grid being populated.</param>
    /// <param name="errors">Aggregate error list.</param>
    private static void ProcessExecutionResult(
        RuleFlowExecutionResult executionResult,
        RuleLaneState laneState,
        PendingRuleWorkItem workItem,
        ProjectFlowResult[] projectResults,
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

    /// <summary>
    /// Runs one rule flow and always releases the acquired concurrency lease.
    /// </summary>
    /// <param name="lease">Concurrency lease that guards the execution slot.</param>
    /// <param name="repositoryRootPath">Repository root path that the rule-flow runner should analyze.</param>
    /// <param name="ruleKey">Rule key being executed.</param>
    /// <param name="ruleMarkdown">Rule markdown prompt or definition content.</param>
    /// <param name="taskItem">Task item assigned to the rule flow.</param>
    /// <param name="cancellationToken">Cancels rule-flow execution.</param>
    /// <returns>The wrapped rule-flow execution result.</returns>
    private async Task<RuleFlowExecutionResult> RunRuleFlowAsync(
        IAsyncDisposable lease,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
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

    /// <summary>
    /// Identifies one queued task item bound to one project and task-item slot.
    /// </summary>
    /// <param name="projectIndex">Project index inside the overall result grid.</param>
    /// <param name="taskItemIndex">Task-item index inside the project.</param>
    /// <param name="taskItem">Stored task item that should be executed.</param>
    private sealed class PendingRuleWorkItem(int projectIndex, int taskItemIndex, StoredTaskItem taskItem)
    {
        public int ProjectIndex { get; } = projectIndex;

        public int TaskItemIndex { get; } = taskItemIndex;

        public StoredTaskItem TaskItem { get; } = taskItem;
    }

    /// <summary>
    /// Holds the queue and execution state for one rule-specific scheduling lane.
    /// </summary>
    /// <param name="ruleIndex">Rule index inside the caller-provided rule definition list.</param>
    /// <param name="ruleDefinition">Rule definition executed by this lane.</param>
    private sealed class RuleLaneState(int ruleIndex, RuleDefinition ruleDefinition)
    {
        private readonly Queue<PendingRuleWorkItem> _queue = [];

        public int RuleIndex { get; } = ruleIndex;

        public RuleDefinition RuleDefinition { get; } = ruleDefinition;

        public bool IsRunning { get; set; }

        public int QueueCount => _queue.Count;

        /// <summary>
        /// Queues one work item for this rule lane.
        /// </summary>
        /// <param name="workItem">Work item to enqueue.</param>
        public void Enqueue(PendingRuleWorkItem workItem) => _queue.Enqueue(workItem);

        /// <summary>
        /// Dequeues the next work item for this rule lane.
        /// </summary>
        /// <returns>The next queued work item.</returns>
        public PendingRuleWorkItem Dequeue() => _queue.Dequeue();
    }

    /// <summary>
    /// Tracks one in-flight rule-flow execution together with its owning lane and work item.
    /// </summary>
    /// <param name="laneState">Lane that owns the in-flight execution.</param>
    /// <param name="workItem">Work item being executed.</param>
    /// <param name="executionTask">Task that completes with the rule-flow execution result.</param>
    private sealed class RunningRuleFlow(
        RuleLaneState laneState,
        PendingRuleWorkItem workItem,
        Task<RuleFlowExecutionResult> executionTask)
    {
        public RuleLaneState LaneState { get; } = laneState;

        public PendingRuleWorkItem WorkItem { get; } = workItem;

        public Task<RuleFlowExecutionResult> ExecutionTask { get; } = executionTask;
    }

    /// <summary>
    /// Wraps the fluent result returned by one rule-flow execution.
    /// </summary>
    /// <param name="result">Rule-flow result to expose.</param>
    private sealed class RuleFlowExecutionResult(Result<RuleFlowWorkflowResult> result)
    {
        public Result<RuleFlowWorkflowResult> Result { get; } = result;
    }
}
