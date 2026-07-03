using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction.Shrinking;

public sealed class MessageShrinkResult
{
    public static MessageShrinkResult NoChange(IReadOnlyList<ChatMessage> messages) => new()
    {
        Messages = messages,
    };

    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    public int FreedEstimatedTokens { get; init; }

    public int ShrunkToolResultCount { get; init; }

    public bool WasChanged => FreedEstimatedTokens > 0 || ShrunkToolResultCount > 0;
}
