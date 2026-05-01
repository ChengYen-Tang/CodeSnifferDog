using CodeSnifferDog.Models.ContextCompaction;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public interface IOperationalContextCompactionCleanupHandler
{
    ValueTask CleanupAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken);
}
