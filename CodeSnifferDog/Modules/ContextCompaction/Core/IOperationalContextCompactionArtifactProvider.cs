using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public interface IOperationalContextCompactionArtifactProvider
{
    ValueTask<OperationalContextCompactionArtifacts> CreateArtifactsAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> messagesToKeep,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken);
}
