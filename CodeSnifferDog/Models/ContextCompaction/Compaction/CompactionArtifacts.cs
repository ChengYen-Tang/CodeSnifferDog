using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

/// <summary>
/// Holds preserved artifact messages carried through compaction.
/// </summary>
public sealed class CompactionArtifacts
{
    /// <summary>
    /// Gets an empty artifact bundle with no attachment or hook-result messages.
    /// </summary>
    public static CompactionArtifacts Empty { get; } = new()
    {
        AttachmentMessages = [],
        HookResultMessages = [],
    };

    /// <summary>
    /// Gets preserved attachment messages emitted alongside the compacted transcript.
    /// </summary>
    public required IReadOnlyList<ChatMessage> AttachmentMessages { get; init; }

    /// <summary>
    /// Gets preserved hook-result messages emitted alongside the compacted transcript.
    /// </summary>
    public required IReadOnlyList<ChatMessage> HookResultMessages { get; init; }
}
