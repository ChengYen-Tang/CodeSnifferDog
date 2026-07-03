using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public interface ICompactionArtifactsProvider
{
    ValueTask<CompactionArtifacts> GetArtifactsAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> messagesToKeep,
        string normalizedSummary,
        CompactionReason reason,
        CancellationToken cancellationToken);
}
