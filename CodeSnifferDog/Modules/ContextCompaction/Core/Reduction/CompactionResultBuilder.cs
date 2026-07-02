using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

internal sealed class CompactionResultBuilder(
    OperationalContextCompactionOptions options,
    IOperationalContextCompactionArtifactsProvider? artifactsProvider)
{
    public async Task<OperationalContextCompactionResult> CreateResultAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        string normalizedSummary,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        List<ChatMessage> preservedSystemMessages = [.. originalMessages.Where(static message => message.Role == ChatRole.System)];
        List<ChatMessage> nonSystemMessages = [.. originalMessages.Where(static message => message.Role != ChatRole.System)];
        List<ChatMessage> messagesToKeep = SelectMessagesToKeep(nonSystemMessages);
        List<OperationalContextCompactionMessageReference> messageReferences = CreateMessageReferences(nonSystemMessages, messagesToKeep);
        List<OperationalContextCompactionMessageReference> archivedMessageReferences = CreateArchivedMessageReferences(nonSystemMessages, messagesToKeep);
        OperationalContextContinuityState continuityState = OperationalContextContinuityStateBuilder.Build(normalizedSummary);
        OperationalContextCompactionArtifacts artifacts = artifactsProvider is null
            ? OperationalContextCompactionArtifacts.Empty
            : await artifactsProvider.GetArtifactsAsync(
                originalMessages,
                messagesToKeep,
                normalizedSummary,
                reason,
                cancellationToken).ConfigureAwait(false);

        ChatMessage boundaryMessage = CreateBoundaryMessage(originalMessages, messageReferences, normalizedSummary, reason);
        ChatMessage summaryMessage = CreateSummaryMessage(normalizedSummary, reason, messagesToKeep.Count > 0);
        ChatMessage continuityStateMessage = OperationalContextContinuityStateBuilder.CreateMessage(continuityState, reason);
        boundaryMessage.AdditionalProperties![OperationalContextCompactionArtifactMetadata.AttachmentsCountKey] = artifacts.AttachmentMessages.Count;
        boundaryMessage.AdditionalProperties![OperationalContextCompactionArtifactMetadata.HookResultsCountKey] = artifacts.HookResultMessages.Count;

        return new OperationalContextCompactionResult
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

    public static OperationalContextCompactionResult CreatePassthroughResult(IReadOnlyList<ChatMessage> messages) => new()
    {
        WasCompacted = false,
        PreservedSystemMessages = [],
        BoundaryMessage = new ChatMessage(ChatRole.System, string.Empty),
        SummaryMessage = new ChatMessage(ChatRole.Assistant, string.Empty),
        ContinuityStateMessage = new ChatMessage(ChatRole.System, string.Empty),
        ContinuityState = new OperationalContextContinuityState(),
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

    private static ChatMessage CreateSummaryMessage(string summary, OperationalContextCompactionReason reason, bool hasPreservedTail)
    {
        ChatMessage summaryMessage = new(
            ChatRole.Assistant,
            $"Operational summary checkpoint{Environment.NewLine}{Environment.NewLine}{summary}")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [OperationalContextCompactionArtifactMetadata.ArtifactKindKey] = OperationalContextCompactionArtifactMetadata.SummaryArtifactKind,
                [OperationalContextCompactionArtifactMetadata.CompactionReasonKey] = reason.ToString(),
                [OperationalContextCompactionArtifactMetadata.SummaryFormatVersionKey] = OperationalContextCompactionArtifactMetadata.CurrentSummaryFormatVersion,
                [OperationalContextCompactionArtifactMetadata.IsCompactionSummaryKey] = true,
                [OperationalContextCompactionArtifactMetadata.HasPreservedTailKey] = hasPreservedTail,
            },
        };

        return summaryMessage;
    }

    private static ChatMessage CreateBoundaryMessage(
        IReadOnlyList<ChatMessage> originalMessages,
        List<OperationalContextCompactionMessageReference> messageReferences,
        string summary,
        OperationalContextCompactionReason reason)
    {
        ChatMessage boundaryMessage = new(
            ChatRole.System,
            "Operational compact boundary");

        OperationalContextCompactionMessageReference? tailReference = messageReferences.Count > 0 ? messageReferences[^1] : null;
        OperationalContextCompactionMessageReference? headReference = messageReferences.Count > 0 ? messageReferences[0] : null;

        boundaryMessage.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            [OperationalContextCompactionArtifactMetadata.ArtifactKindKey] = OperationalContextCompactionArtifactMetadata.BoundaryArtifactKind,
            [OperationalContextCompactionArtifactMetadata.CompactionReasonKey] = reason.ToString(),
            [OperationalContextCompactionArtifactMetadata.MessagesToKeepCountKey] = messageReferences.Count,
            [OperationalContextCompactionArtifactMetadata.BoundarySummaryKey] = summary,
            [OperationalContextCompactionArtifactMetadata.PreservedTailCountKey] = messageReferences.Count,
            [OperationalContextCompactionArtifactMetadata.PreservedTailIndexesKey] = messageReferences.Select(static reference => reference.MessageIndex).ToArray(),
            [OperationalContextCompactionArtifactMetadata.PreservedTailTextsKey] = messageReferences.Select(static reference => reference.Text).ToArray(),
            [OperationalContextCompactionArtifactMetadata.PreservedTailIdsKey] = messageReferences.Select(static reference => reference.MessageId ?? string.Empty).ToArray(),
        };

        if (headReference is not null)
        {
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.PreservedSegmentHeadIndexKey] = headReference.MessageIndex;
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.PreservedSegmentHeadIdKey] = headReference.MessageId ?? string.Empty;
        }

        if (tailReference is not null)
        {
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.PreservedSegmentTailIndexKey] = tailReference.MessageIndex;
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.PreservedSegmentTailIdKey] = tailReference.MessageId ?? string.Empty;
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorIndexKey] = tailReference.MessageIndex;
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorIdKey] = tailReference.MessageId ?? string.Empty;
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorRoleKey] = tailReference.Role.ToString();
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorTextKey] = tailReference.Text;
        }
        else if (originalMessages.Count > 0)
        {
            ChatMessage lastOriginalMessage = originalMessages[^1];
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorIndexKey] = originalMessages.Count - 1;
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorRoleKey] = lastOriginalMessage.Role.ToString();
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorTextKey] = lastOriginalMessage.Text ?? string.Empty;
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

    private static List<OperationalContextCompactionMessageReference> CreateMessageReferences(
        List<ChatMessage> sourceMessages,
        List<ChatMessage> keptMessages)
    {
        List<OperationalContextCompactionMessageReference> references = [];

        for (int index = 0; index < sourceMessages.Count; index++)
        {
            if (!keptMessages.Contains(sourceMessages[index]))
                continue;

            references.Add(new OperationalContextCompactionMessageReference
            {
                MessageIndex = index,
                MessageId = GetMessageIdentity(sourceMessages[index], index),
                Role = sourceMessages[index].Role,
                Text = sourceMessages[index].Text ?? string.Empty,
            });
        }

        return references;
    }

    private static List<OperationalContextCompactionMessageReference> CreateArchivedMessageReferences(
        List<ChatMessage> sourceMessages,
        List<ChatMessage> keptMessages)
    {
        List<OperationalContextCompactionMessageReference> references = [];

        for (int index = 0; index < sourceMessages.Count; index++)
        {
            if (keptMessages.Contains(sourceMessages[index]))
                continue;

            references.Add(new OperationalContextCompactionMessageReference
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

        if (message.AdditionalProperties.TryGetValue(OperationalContextCompactionArtifactMetadata.MessageIdentityKey, out object? existingValue) &&
            existingValue is string existingId &&
            !string.IsNullOrWhiteSpace(existingId))
            return existingId;

        string generatedId = $"{index:D8}:{message.Role}:{Guid.NewGuid():N}";
        message.AdditionalProperties[OperationalContextCompactionArtifactMetadata.MessageIdentityKey] = generatedId;
        return generatedId;
    }
}
