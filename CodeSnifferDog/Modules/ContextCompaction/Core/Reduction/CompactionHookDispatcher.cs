using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

internal sealed class CompactionHookDispatcher(
    IEnumerable<IHook>? hooks,
    IEnumerable<ICleanupHandler>? cleanupHandlers)
{
    private readonly IReadOnlyList<ICleanupHandler> _cleanupHandlers =
        cleanupHandlers?.ToArray() ?? [];
    private readonly IReadOnlyList<IHook> _hooks = hooks?.ToArray() ?? [];

    public async ValueTask RunBeforeCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        CompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (IHook hook in _hooks)
            await hook.OnBeforeCompactionAsync(originalMessages, reason, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RunAfterCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        CompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (IHook hook in _hooks)
            await hook.OnAfterCompactionAsync(originalMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RunCleanupAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        CompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (ICleanupHandler cleanupHandler in _cleanupHandlers)
            await cleanupHandler.CleanupAsync(originalMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
    }
}
