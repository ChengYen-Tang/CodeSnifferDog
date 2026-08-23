using Microsoft.Agents.AI.Workflows;
using System.Runtime.ExceptionServices;
using AgentFrameworkWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace CodeSnifferDog.Workflows.Adapters.AgentFramework.Runtime;

/// <summary>
/// Executes existing domain workflows as single Agent Framework executors without replacing their internal state machines.
/// </summary>
/// <remarks>
/// The boundary emits standard Agent Framework events and can create checkpoints at executor boundaries. Existing
/// workflow retry, snapshot, queue, lease, and domain-state behavior remains owned by CodeSnifferDog.
/// </remarks>
internal sealed class WorkflowRuntime : IWorkflowRuntime
{
    /// <inheritdoc />
    public async Task<TOutput> RunAsync<TInput, TOutput>(
        string executorId,
        TInput input,
        Func<TInput, CancellationToken, Task<TOutput>> operation,
        CancellationToken cancellationToken = default)
        where TInput : notnull
        where TOutput : notnull =>
        (await RunWithEventsAsync(
            executorId,
            input,
            operation,
            options: null,
            cancellationToken).ConfigureAwait(false)).Output;

    /// <inheritdoc />
    public async Task<WorkflowRunResult<TOutput>> RunWithEventsAsync<TInput, TOutput>(
        string executorId,
        TInput input,
        Func<TInput, CancellationToken, Task<TOutput>> operation,
        WorkflowRunOptions? options = null,
        CancellationToken cancellationToken = default)
        where TInput : notnull
        where TOutput : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executorId);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        DelegatingExecutor<TInput, TOutput> executor = new(
            executorId.Trim(),
            operation,
            cancellationToken);
        AgentFrameworkWorkflow workflow = new WorkflowBuilder(executor)
            .WithOutputFrom(executor)
            .Build();

        await using StreamingRun run = await StartAsync(workflow, input, options, cancellationToken)
            .ConfigureAwait(false);

        List<WorkflowEvent> events = [];
        TOutput? output = default;
        bool hasOutput = false;
        Exception? failure = null;

        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(workflowEvent);

            switch (workflowEvent)
            {
                case WorkflowOutputEvent { Data: TOutput value }:
                    if (hasOutput)
                    {
                        throw new InvalidOperationException(
                            $"Workflow executor '{executorId}' produced more than one output.");
                    }

                    output = value;
                    hasOutput = true;
                    break;

                case WorkflowErrorEvent { Exception: not null } workflowError:
                    failure ??= workflowError.Exception;
                    break;

                case ExecutorFailedEvent { Data: Exception executorError }:
                    failure ??= executorError;
                    break;
            }
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
            throw new InvalidOperationException("The workflow runtime reached an unreachable failure state.");
        }

        if (!hasOutput)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException(
                $"Workflow executor '{executorId}' completed without producing an output.");
        }

        return new WorkflowRunResult<TOutput>(output!, events, [.. run.Checkpoints]);
    }

    private static ValueTask<StreamingRun> StartAsync<TInput>(
        AgentFrameworkWorkflow workflow,
        TInput input,
        WorkflowRunOptions? options,
        CancellationToken cancellationToken)
        where TInput : notnull =>
        options?.CheckpointManager is { } checkpointManager
            ? InProcessExecution.RunStreamingAsync(
                workflow,
                input,
                checkpointManager,
                options.SessionId,
                cancellationToken)
            : InProcessExecution.RunStreamingAsync(
                workflow,
                input,
                options?.SessionId,
                cancellationToken);
}
