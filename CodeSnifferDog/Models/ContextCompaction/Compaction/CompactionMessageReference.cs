using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

/// <summary>
/// References one transcript message by index, identity, role, and text snapshot.
/// </summary>
public sealed class CompactionMessageReference
{
    /// <summary>
    /// Gets the message index in the original transcript.
    /// </summary>
    public required int MessageIndex { get; init; }

    /// <summary>
    /// Gets the optional message identifier.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Gets the message role.
    /// </summary>
    public required ChatRole Role { get; init; }

    /// <summary>
    /// Gets the captured message text.
    /// </summary>
    public required string Text { get; init; }
}
