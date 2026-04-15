using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextCompactionResult
{
    public required IReadOnlyList<ChatMessage> PreservedSystemMessages { get; init; }

    public required ChatMessage BoundaryMessage { get; init; }

    public required ChatMessage SummaryMessage { get; init; }

    public required IReadOnlyList<ChatMessage> MessagesToKeep { get; init; }

    public required IReadOnlyList<OperationalContextCompactionMessageReference> MessageReferences { get; init; }

    public required IReadOnlyList<ChatMessage> AttachmentMessages { get; init; }

    public required IReadOnlyList<ChatMessage> HookResultMessages { get; init; }
}
