using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;

internal sealed class ReactiveRetryPreparation
{
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
}
