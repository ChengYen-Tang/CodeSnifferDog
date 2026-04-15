using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public interface IOperationalContextCompactionHook
{
    ValueTask OnBeforeCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken);

    ValueTask OnAfterCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken);
}
