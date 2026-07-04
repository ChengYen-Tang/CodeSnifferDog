using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

/// <summary>
/// Performs cleanup after a compaction pass has completed successfully.
/// </summary>
public interface ICleanupHandler
{
    /// <summary>
    /// Cleans up any external state associated with the original transcript after compaction succeeds.
    /// </summary>
    /// <param name="originalMessages">Original message history before compaction.</param>
    /// <param name="compactedMessages">Compacted message history that replaced the original transcript.</param>
    /// <param name="reason">Reason the current compaction pass was triggered.</param>
    /// <param name="cancellationToken">Cancels cleanup execution.</param>
    ValueTask CleanupAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        CompactionReason reason,
        CancellationToken cancellationToken);
}
