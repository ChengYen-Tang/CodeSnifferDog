using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public sealed class MetadataCompactionArtifactsProvider(
    CompactionOptions options) : ICompactionArtifactsProvider
{
    private readonly CompactionOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<CompactionArtifacts> GetArtifactsAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> messagesToKeep,
        string normalizedSummary,
        CompactionReason reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalMessages);
        ArgumentNullException.ThrowIfNull(messagesToKeep);

        HashSet<ChatMessage> keptMessages = [.. messagesToKeep];
        List<ChatMessage> attachmentMessages = [];
        List<ChatMessage> hookResultMessages = [];
        int usedTokens = 0;

        foreach (ChatMessage message in originalMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (keptMessages.Contains(message))
                continue;

            string? artifactKind = message.AdditionalProperties?
                .GetValueOrDefault(CompactionArtifactMetadata.ArtifactKindKey)?
                .ToString();

            if (artifactKind is not (
                CompactionArtifactMetadata.AttachmentArtifactKind or
                CompactionArtifactMetadata.HookResultArtifactKind))
                continue;

            int messageTokens = TokenEstimator.Estimate([message]);
            if (usedTokens + messageTokens > _options.PostCompactAttachmentTokenBudget)
                break;

            usedTokens += messageTokens;

            if (artifactKind == CompactionArtifactMetadata.AttachmentArtifactKind)
                attachmentMessages.Add(message);
            else
                hookResultMessages.Add(message);
        }

        return ValueTask.FromResult(new CompactionArtifacts
        {
            AttachmentMessages = attachmentMessages,
            HookResultMessages = hookResultMessages,
        });
    }
}
