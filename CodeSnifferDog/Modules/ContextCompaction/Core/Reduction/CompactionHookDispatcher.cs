using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

/// <summary>
/// Executes compaction hooks and cleanup handlers in registration order.
/// </summary>
/// <param name="hooks">Hooks that observe the compaction lifecycle.</param>
/// <param name="cleanupHandlers">Cleanup handlers that run after successful compaction.</param>
internal sealed class CompactionHookDispatcher(
    IEnumerable<IHook>? hooks,
    IEnumerable<ICleanupHandler>? cleanupHandlers)
{
    private readonly IReadOnlyList<ICleanupHandler> _cleanupHandlers =
        cleanupHandlers?.ToArray() ?? [];
    private readonly IReadOnlyList<IHook> _hooks = hooks?.ToArray() ?? [];

    /// <summary>
    /// Runs all registered pre-compaction hooks.
    /// </summary>
    /// <param name="originalMessages">Original message history before compaction.</param>
    /// <param name="reason">Reason the current compaction pass was triggered.</param>
    /// <param name="cancellationToken">Cancels hook execution.</param>
    public async ValueTask RunBeforeCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        CompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (IHook hook in _hooks)
            await hook.OnBeforeCompactionAsync(originalMessages, reason, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs all registered post-compaction hooks.
    /// </summary>
    /// <param name="originalMessages">Original message history before compaction.</param>
    /// <param name="compactedMessages">Compacted message history that replaced the original transcript.</param>
    /// <param name="reason">Reason the current compaction pass was triggered.</param>
    /// <param name="cancellationToken">Cancels hook execution.</param>
    public async ValueTask RunAfterCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        CompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (IHook hook in _hooks)
            await hook.OnAfterCompactionAsync(originalMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs all registered cleanup handlers after compaction succeeds.
    /// </summary>
    /// <param name="originalMessages">Original message history before compaction.</param>
    /// <param name="compactedMessages">Compacted message history that replaced the original transcript.</param>
    /// <param name="reason">Reason the current compaction pass was triggered.</param>
    /// <param name="cancellationToken">Cancels cleanup execution.</param>
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
