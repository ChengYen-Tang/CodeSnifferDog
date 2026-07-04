using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction.Shrinking;

/// <summary>
/// Describes the result of local message shrinking before full transcript compaction.
/// </summary>
public sealed class MessageShrinkResult
{
    /// <summary>
    /// Creates a result that reports no shrink changes.
    /// </summary>
    /// <param name="messages">Original messages preserved without changes.</param>
    /// <returns>A no-change shrink result.</returns>
    public static MessageShrinkResult NoChange(IReadOnlyList<ChatMessage> messages) => new()
    {
        Messages = messages,
    };

    /// <summary>
    /// Gets the messages after shrinking.
    /// </summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>
    /// Gets the estimated number of tokens freed by shrinking.
    /// </summary>
    public int FreedEstimatedTokens { get; init; }

    /// <summary>
    /// Gets how many tool-result messages were shrunk.
    /// </summary>
    public int ShrunkToolResultCount { get; init; }

    /// <summary>
    /// Gets whether shrinking changed the message set in a meaningful way.
    /// </summary>
    public bool WasChanged => FreedEstimatedTokens > 0 || ShrunkToolResultCount > 0;
}
