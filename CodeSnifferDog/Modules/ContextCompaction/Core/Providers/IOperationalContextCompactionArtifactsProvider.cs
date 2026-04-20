using CodeSnifferDog.Models.ContextCompaction;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public interface IOperationalContextCompactionArtifactsProvider
{
    ValueTask<OperationalContextCompactionArtifacts> GetArtifactsAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> messagesToKeep,
        string normalizedSummary,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken);
}
