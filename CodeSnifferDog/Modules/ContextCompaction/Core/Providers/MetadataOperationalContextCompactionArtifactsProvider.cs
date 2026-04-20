using CodeSnifferDog.Models.ContextCompaction;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public sealed class MetadataOperationalContextCompactionArtifactsProvider(
    OperationalContextCompactionOptions options) : IOperationalContextCompactionArtifactsProvider
{
    private readonly OperationalContextCompactionOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<OperationalContextCompactionArtifacts> GetArtifactsAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> messagesToKeep,
        string normalizedSummary,
        OperationalContextCompactionReason reason,
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
                .GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?
                .ToString();

            if (artifactKind is not (
                OperationalContextCompactionArtifactMetadata.AttachmentArtifactKind or
                OperationalContextCompactionArtifactMetadata.HookResultArtifactKind))
                continue;

            int messageTokens = OperationalContextTokenEstimator.Estimate([message]);
            if (usedTokens + messageTokens > _options.PostCompactAttachmentTokenBudget)
                break;

            usedTokens += messageTokens;

            if (artifactKind == OperationalContextCompactionArtifactMetadata.AttachmentArtifactKind)
                attachmentMessages.Add(message);
            else
                hookResultMessages.Add(message);
        }

        return ValueTask.FromResult(new OperationalContextCompactionArtifacts
        {
            AttachmentMessages = attachmentMessages,
            HookResultMessages = hookResultMessages,
        });
    }
}
