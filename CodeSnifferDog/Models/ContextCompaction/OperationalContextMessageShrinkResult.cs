using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextMessageShrinkResult
{
    public static OperationalContextMessageShrinkResult NoChange(IReadOnlyList<ChatMessage> messages) => new()
    {
        Messages = messages,
    };

    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    public int FreedEstimatedTokens { get; init; }

    public int ShrunkToolResultCount { get; init; }

    public bool WasChanged => FreedEstimatedTokens > 0 || ShrunkToolResultCount > 0;
}
