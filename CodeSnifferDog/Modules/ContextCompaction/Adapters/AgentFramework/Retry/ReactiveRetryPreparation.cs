using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;

/// <summary>
/// Holds the prepared message list used for a reactive retry attempt.
/// </summary>
internal sealed class ReactiveRetryPreparation
{
    /// <summary>
    /// Gets the retry messages prepared for the next invocation attempt.
    /// </summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
}
