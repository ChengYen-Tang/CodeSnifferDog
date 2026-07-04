using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

/// <summary>
/// Holds the rewritten transcript and supporting artifacts produced by one compaction pass.
/// </summary>
public sealed class CompactionResult
{
    /// <summary>
    /// Gets whether compaction actually rewrote the transcript.
    /// </summary>
    public required bool WasCompacted { get; init; }

    /// <summary>
    /// Gets preserved system messages that remain at the head of the transcript.
    /// </summary>
    public required IReadOnlyList<ChatMessage> PreservedSystemMessages { get; init; }

    /// <summary>
    /// Gets the synthetic boundary message inserted by compaction.
    /// </summary>
    public required ChatMessage BoundaryMessage { get; init; }

    /// <summary>
    /// Gets the generated summary message inserted by compaction.
    /// </summary>
    public required ChatMessage SummaryMessage { get; init; }

    /// <summary>
    /// Gets the serialized continuity-state message inserted by compaction.
    /// </summary>
    public required ChatMessage ContinuityStateMessage { get; init; }

    /// <summary>
    /// Gets the structured continuity state extracted during compaction.
    /// </summary>
    public required ContinuityState ContinuityState { get; init; }

    /// <summary>
    /// Gets the transcript messages kept after compaction.
    /// </summary>
    public required IReadOnlyList<ChatMessage> MessagesToKeep { get; init; }

    /// <summary>
    /// Gets references to transcript messages kept after compaction.
    /// </summary>
    public required IReadOnlyList<CompactionMessageReference> MessageReferences { get; init; }

    /// <summary>
    /// Gets references to transcript messages archived by compaction.
    /// </summary>
    public required IReadOnlyList<CompactionMessageReference> ArchivedMessageReferences { get; init; }

    /// <summary>
    /// Gets preserved attachment messages emitted alongside the compacted transcript.
    /// </summary>
    public required IReadOnlyList<ChatMessage> AttachmentMessages { get; init; }

    /// <summary>
    /// Gets preserved hook-result messages emitted alongside the compacted transcript.
    /// </summary>
    public required IReadOnlyList<ChatMessage> HookResultMessages { get; init; }
}
