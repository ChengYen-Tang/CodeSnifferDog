using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextCompactionResult
{
    public required bool WasCompacted { get; init; }

    public required IReadOnlyList<ChatMessage> PreservedSystemMessages { get; init; }

    public required ChatMessage BoundaryMessage { get; init; }

    public required ChatMessage SummaryMessage { get; init; }

    public required ChatMessage ContinuityStateMessage { get; init; }

    public required OperationalContextContinuityState ContinuityState { get; init; }

    public required IReadOnlyList<ChatMessage> MessagesToKeep { get; init; }

    public required IReadOnlyList<OperationalContextCompactionMessageReference> MessageReferences { get; init; }

    public required IReadOnlyList<OperationalContextCompactionMessageReference> ArchivedMessageReferences { get; init; }

    public required IReadOnlyList<ChatMessage> AttachmentMessages { get; init; }

    public required IReadOnlyList<ChatMessage> HookResultMessages { get; init; }
}
