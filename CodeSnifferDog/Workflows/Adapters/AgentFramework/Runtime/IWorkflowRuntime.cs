namespace CodeSnifferDog.Workflows.Adapters.AgentFramework.Runtime;

/// <summary>
/// Runs one existing domain workflow inside an Agent Framework executor boundary.
/// </summary>
internal interface IWorkflowRuntime
{
    /// <summary>
    /// Runs the supplied operation and returns its domain result.
    /// </summary>
    Task<TOutput> RunAsync<TInput, TOutput>(
        string executorId,
        TInput input,
        Func<TInput, CancellationToken, Task<TOutput>> operation,
        CancellationToken cancellationToken = default)
        where TInput : notnull
        where TOutput : notnull;

    /// <summary>
    /// Runs the supplied operation and returns its domain result together with Agent Framework events and checkpoints.
    /// </summary>
    Task<WorkflowRunResult<TOutput>> RunWithEventsAsync<TInput, TOutput>(
        string executorId,
        TInput input,
        Func<TInput, CancellationToken, Task<TOutput>> operation,
        WorkflowRunOptions? options = null,
        CancellationToken cancellationToken = default)
        where TInput : notnull
        where TOutput : notnull;
}
