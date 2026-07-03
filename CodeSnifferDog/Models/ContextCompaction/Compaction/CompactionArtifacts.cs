using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

public sealed class CompactionArtifacts
{
    public static CompactionArtifacts Empty { get; } = new()
    {
        AttachmentMessages = [],
        HookResultMessages = [],
    };

    public required IReadOnlyList<ChatMessage> AttachmentMessages { get; init; }

    public required IReadOnlyList<ChatMessage> HookResultMessages { get; init; }
}
