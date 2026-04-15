using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextCompactionArtifacts
{
    public static OperationalContextCompactionArtifacts Empty { get; } = new()
    {
        AttachmentMessages = [],
        HookResultMessages = [],
    };

    public required IReadOnlyList<ChatMessage> AttachmentMessages { get; init; }

    public required IReadOnlyList<ChatMessage> HookResultMessages { get; init; }
}
