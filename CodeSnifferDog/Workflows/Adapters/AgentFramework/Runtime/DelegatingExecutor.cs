using Microsoft.Agents.AI.Workflows;

namespace CodeSnifferDog.Workflows.Adapters.AgentFramework.Runtime;

/// <summary>
/// Adapts one existing asynchronous domain operation to an Agent Framework executor.
/// </summary>
internal sealed class DelegatingExecutor<TInput, TOutput>(
    string id,
    Func<TInput, CancellationToken, Task<TOutput>> operation,
    CancellationToken callerCancellationToken)
    : Executor<TInput, TOutput>(id)
    where TInput : notnull
    where TOutput : notnull
{
    private readonly Func<TInput, CancellationToken, Task<TOutput>> _operation =
        operation ?? throw new ArgumentNullException(nameof(operation));
    private readonly CancellationToken _callerCancellationToken = callerCancellationToken;

    /// <inheritdoc />
    public override async ValueTask<TOutput> HandleAsync(
        TInput message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _callerCancellationToken.ThrowIfCancellationRequested();
        return await _operation(message, _callerCancellationToken).ConfigureAwait(false);
    }
}
