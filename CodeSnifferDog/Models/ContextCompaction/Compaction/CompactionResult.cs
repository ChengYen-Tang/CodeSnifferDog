using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

public sealed class CompactionResult
{
    public required bool WasCompacted { get; init; }

    public required IReadOnlyList<ChatMessage> PreservedSystemMessages { get; init; }

    public required ChatMessage BoundaryMessage { get; init; }

    public required ChatMessage SummaryMessage { get; init; }

    public required ChatMessage ContinuityStateMessage { get; init; }

    public required ContinuityState ContinuityState { get; init; }

    public required IReadOnlyList<ChatMessage> MessagesToKeep { get; init; }

    public required IReadOnlyList<CompactionMessageReference> MessageReferences { get; init; }

    public required IReadOnlyList<CompactionMessageReference> ArchivedMessageReferences { get; init; }

    public required IReadOnlyList<ChatMessage> AttachmentMessages { get; init; }

    public required IReadOnlyList<ChatMessage> HookResultMessages { get; init; }
}
