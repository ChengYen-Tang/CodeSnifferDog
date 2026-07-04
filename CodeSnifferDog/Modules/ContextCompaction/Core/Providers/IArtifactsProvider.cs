using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

/// <summary>
/// Produces carry-forward artifact messages that should survive a compaction pass.
/// </summary>
public interface ICompactionArtifactsProvider
{
    /// <summary>
    /// Selects artifact messages that should be reattached to the compacted transcript.
    /// </summary>
    /// <param name="originalMessages">Complete message history before compaction.</param>
    /// <param name="messagesToKeep">Non-system messages that will remain in the compacted tail.</param>
    /// <param name="normalizedSummary">Validated summary text generated for the compacted history.</param>
    /// <param name="reason">Reason the current compaction pass was triggered.</param>
    /// <param name="cancellationToken">Cancels artifact selection.</param>
    /// <returns>The artifact messages that should be preserved alongside the summary and continuity state.</returns>
    ValueTask<CompactionArtifacts> GetArtifactsAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> messagesToKeep,
        string normalizedSummary,
        CompactionReason reason,
        CancellationToken cancellationToken);
}
