using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

/// <summary>
/// Observes compaction passes before and after transcript rewriting.
/// </summary>
public interface IHook
{
    /// <summary>
    /// Runs after the transcript has been selected for compaction but before summary generation begins.
    /// </summary>
    /// <param name="originalMessages">Original message history that will be compacted.</param>
    /// <param name="reason">Reason the current compaction pass was triggered.</param>
    /// <param name="cancellationToken">Cancels hook execution.</param>
    ValueTask OnBeforeCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        CompactionReason reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs after the compacted transcript has been built.
    /// </summary>
    /// <param name="originalMessages">Original message history before compaction.</param>
    /// <param name="compactedMessages">Rewritten message history that will remain active.</param>
    /// <param name="reason">Reason the current compaction pass was triggered.</param>
    /// <param name="cancellationToken">Cancels hook execution.</param>
    ValueTask OnAfterCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        CompactionReason reason,
        CancellationToken cancellationToken);
}
