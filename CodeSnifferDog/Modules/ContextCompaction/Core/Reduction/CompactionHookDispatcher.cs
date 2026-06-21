using CodeSnifferDog.Models.ContextCompaction;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

internal sealed class CompactionHookDispatcher(
    IEnumerable<IOperationalContextCompactionHook>? hooks,
    IEnumerable<IOperationalContextCompactionCleanupHandler>? cleanupHandlers)
{
    private readonly IReadOnlyList<IOperationalContextCompactionCleanupHandler> _cleanupHandlers =
        cleanupHandlers?.ToArray() ?? [];
    private readonly IReadOnlyList<IOperationalContextCompactionHook> _hooks = hooks?.ToArray() ?? [];

    public async ValueTask RunBeforeCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (IOperationalContextCompactionHook hook in _hooks)
            await hook.OnBeforeCompactionAsync(originalMessages, reason, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RunAfterCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (IOperationalContextCompactionHook hook in _hooks)
            await hook.OnAfterCompactionAsync(originalMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RunCleanupAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (IOperationalContextCompactionCleanupHandler cleanupHandler in _cleanupHandlers)
            await cleanupHandler.CleanupAsync(originalMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
    }
}
