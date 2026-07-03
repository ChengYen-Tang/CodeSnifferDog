using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

internal sealed class CompactionResultBuilder(
    CompactionOptions options,
    ICompactionArtifactsProvider? artifactsProvider)
{
    public async Task<CompactionResult> CreateResultAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        string normalizedSummary,
        CompactionReason reason,
        CancellationToken cancellationToken)
    {
        List<ChatMessage> preservedSystemMessages = [.. originalMessages.Where(static message => message.Role == ChatRole.System)];
        List<ChatMessage> nonSystemMessages = [.. originalMessages.Where(static message => message.Role != ChatRole.System)];
        List<ChatMessage> messagesToKeep = SelectMessagesToKeep(nonSystemMessages);
        List<CompactionMessageReference> messageReferences = CreateMessageReferences(nonSystemMessages, messagesToKeep);
        List<CompactionMessageReference> archivedMessageReferences = CreateArchivedMessageReferences(nonSystemMessages, messagesToKeep);
        ContinuityState continuityState = ContinuityStateBuilder.Build(normalizedSummary);
        CompactionArtifacts artifacts = artifactsProvider is null
            ? CompactionArtifacts.Empty
            : await artifactsProvider.GetArtifactsAsync(
                originalMessages,
                messagesToKeep,
                normalizedSummary,
                reason,
                cancellationToken).ConfigureAwait(false);

        ChatMessage boundaryMessage = CreateBoundaryMessage(originalMessages, messageReferences, normalizedSummary, reason);
        ChatMessage summaryMessage = CreateSummaryMessage(normalizedSummary, reason, messagesToKeep.Count > 0);
        ChatMessage continuityStateMessage = ContinuityStateBuilder.CreateMessage(continuityState, reason);
        boundaryMessage.AdditionalProperties![CompactionArtifactMetadata.AttachmentsCountKey] = artifacts.AttachmentMessages.Count;
        boundaryMessage.AdditionalProperties![CompactionArtifactMetadata.HookResultsCountKey] = artifacts.HookResultMessages.Count;

        return new CompactionResult
        {
            WasCompacted = true,
            PreservedSystemMessages = preservedSystemMessages,
            BoundaryMessage = boundaryMessage,
            SummaryMessage = summaryMessage,
            ContinuityStateMessage = continuityStateMessage,
            ContinuityState = continuityState,
            MessagesToKeep = messagesToKeep,
            MessageReferences = messageReferences,
            ArchivedMessageReferences = archivedMessageReferences,
            AttachmentMessages = artifacts.AttachmentMessages,
            HookResultMessages = artifacts.HookResultMessages,
        };
    }

    public static CompactionResult CreatePassthroughResult(IReadOnlyList<ChatMessage> messages) => new()
    {
        WasCompacted = false,
        PreservedSystemMessages = [],
        BoundaryMessage = new ChatMessage(ChatRole.System, string.Empty),
        SummaryMessage = new ChatMessage(ChatRole.Assistant, string.Empty),
        ContinuityStateMessage = new ChatMessage(ChatRole.System, string.Empty),
        ContinuityState = new ContinuityState(),
        MessagesToKeep = messages,
        MessageReferences = [],
        ArchivedMessageReferences = [],
        AttachmentMessages = [],
        HookResultMessages = [],
    };

    public static void EnsureMessageIdentities(List<ChatMessage> messages)
    {
        for (int index = 0; index < messages.Count; index++)
            _ = GetMessageIdentity(messages[index], index);
    }

    private static ChatMessage CreateSummaryMessage(string summary, CompactionReason reason, bool hasPreservedTail)
    {
        ChatMessage summaryMessage = new(
            ChatRole.Assistant,
            $"Operational summary checkpoint{Environment.NewLine}{Environment.NewLine}{summary}")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [CompactionArtifactMetadata.ArtifactKindKey] = CompactionArtifactMetadata.SummaryArtifactKind,
                [CompactionArtifactMetadata.CompactionReasonKey] = reason.ToString(),
                [CompactionArtifactMetadata.SummaryFormatVersionKey] = CompactionArtifactMetadata.CurrentSummaryFormatVersion,
                [CompactionArtifactMetadata.IsCompactionSummaryKey] = true,
                [CompactionArtifactMetadata.HasPreservedTailKey] = hasPreservedTail,
            },
        };

        return summaryMessage;
    }

    private static ChatMessage CreateBoundaryMessage(
        IReadOnlyList<ChatMessage> originalMessages,
        List<CompactionMessageReference> messageReferences,
        string summary,
        CompactionReason reason)
    {
        ChatMessage boundaryMessage = new(
            ChatRole.System,
            "Operational compact boundary");

        CompactionMessageReference? tailReference = messageReferences.Count > 0 ? messageReferences[^1] : null;
        CompactionMessageReference? headReference = messageReferences.Count > 0 ? messageReferences[0] : null;

        boundaryMessage.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            [CompactionArtifactMetadata.ArtifactKindKey] = CompactionArtifactMetadata.BoundaryArtifactKind,
            [CompactionArtifactMetadata.CompactionReasonKey] = reason.ToString(),
            [CompactionArtifactMetadata.MessagesToKeepCountKey] = messageReferences.Count,
            [CompactionArtifactMetadata.BoundarySummaryKey] = summary,
            [CompactionArtifactMetadata.PreservedTailCountKey] = messageReferences.Count,
            [CompactionArtifactMetadata.PreservedTailIndexesKey] = messageReferences.Select(static reference => reference.MessageIndex).ToArray(),
            [CompactionArtifactMetadata.PreservedTailTextsKey] = messageReferences.Select(static reference => reference.Text).ToArray(),
            [CompactionArtifactMetadata.PreservedTailIdsKey] = messageReferences.Select(static reference => reference.MessageId ?? string.Empty).ToArray(),
        };

        if (headReference is not null)
        {
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.PreservedSegmentHeadIndexKey] = headReference.MessageIndex;
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.PreservedSegmentHeadIdKey] = headReference.MessageId ?? string.Empty;
        }

        if (tailReference is not null)
        {
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.PreservedSegmentTailIndexKey] = tailReference.MessageIndex;
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.PreservedSegmentTailIdKey] = tailReference.MessageId ?? string.Empty;
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.BoundaryAnchorIndexKey] = tailReference.MessageIndex;
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.BoundaryAnchorIdKey] = tailReference.MessageId ?? string.Empty;
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.BoundaryAnchorRoleKey] = tailReference.Role.ToString();
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.BoundaryAnchorTextKey] = tailReference.Text;
        }
        else if (originalMessages.Count > 0)
        {
            ChatMessage lastOriginalMessage = originalMessages[^1];
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.BoundaryAnchorIndexKey] = originalMessages.Count - 1;
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.BoundaryAnchorRoleKey] = lastOriginalMessage.Role.ToString();
            boundaryMessage.AdditionalProperties[CompactionArtifactMetadata.BoundaryAnchorTextKey] = lastOriginalMessage.Text ?? string.Empty;
        }

        return boundaryMessage;
    }

    private List<ChatMessage> SelectMessagesToKeep(List<ChatMessage> nonSystemMessages)
    {
        if (nonSystemMessages.Count == 0)
            return [];

        List<ChatMessage> keptMessages = [];
        int totalTokens = 0;
        int messageCount = 0;

        for (int index = nonSystemMessages.Count - 1; index >= 0; index--)
        {
            ChatMessage message = nonSystemMessages[index];
            int messageTokens = TokenEstimator.Estimate([message]);

            if (keptMessages.Count > 0 && totalTokens >= options.PreservedTailMaxTokens)
                break;

            keptMessages.Insert(0, message);
            totalTokens += messageTokens;
            messageCount++;

            bool reachedMinimumTail =
                totalTokens >= options.PreservedTailMinTokens &&
                messageCount >= options.PreservedTailMinMessages;

            if (reachedMinimumTail)
                break;
        }

        return keptMessages;
    }

    private static List<CompactionMessageReference> CreateMessageReferences(
        List<ChatMessage> sourceMessages,
        List<ChatMessage> keptMessages)
    {
        List<CompactionMessageReference> references = [];

        for (int index = 0; index < sourceMessages.Count; index++)
        {
            if (!keptMessages.Contains(sourceMessages[index]))
                continue;

            references.Add(new CompactionMessageReference
            {
                MessageIndex = index,
                MessageId = GetMessageIdentity(sourceMessages[index], index),
                Role = sourceMessages[index].Role,
                Text = sourceMessages[index].Text ?? string.Empty,
            });
        }

        return references;
    }

    private static List<CompactionMessageReference> CreateArchivedMessageReferences(
        List<ChatMessage> sourceMessages,
        List<ChatMessage> keptMessages)
    {
        List<CompactionMessageReference> references = [];

        for (int index = 0; index < sourceMessages.Count; index++)
        {
            if (keptMessages.Contains(sourceMessages[index]))
                continue;

            references.Add(new CompactionMessageReference
            {
                MessageIndex = index,
                MessageId = GetMessageIdentity(sourceMessages[index], index),
                Role = sourceMessages[index].Role,
                Text = sourceMessages[index].Text ?? string.Empty,
            });
        }

        return references;
    }

    private static string GetMessageIdentity(ChatMessage message, int index)
    {
        message.AdditionalProperties ??= [];

        if (message.AdditionalProperties.TryGetValue(CompactionArtifactMetadata.MessageIdentityKey, out object? existingValue) &&
            existingValue is string existingId &&
            !string.IsNullOrWhiteSpace(existingId))
            return existingId;

        string generatedId = $"{index:D8}:{message.Role}:{Guid.NewGuid():N}";
        message.AdditionalProperties[CompactionArtifactMetadata.MessageIdentityKey] = generatedId;
        return generatedId;
    }
}
