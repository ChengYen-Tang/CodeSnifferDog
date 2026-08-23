using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

/// <summary>
/// Builds the compacted transcript payload, including preserved tail messages, references, and carry-forward artifacts.
/// </summary>
/// <param name="artifactsProvider">Optional provider that reattaches artifact messages after compaction.</param>
internal sealed class CompactionResultBuilder(ICompactionArtifactsProvider? artifactsProvider)
{
    /// <summary>
    /// Creates a fully compacted result from the original message history and generated summary.
    /// </summary>
    /// <param name="originalMessages">Complete message history before compaction.</param>
    /// <param name="plannedMessagesToKeep">Preselected non-system tail messages that remain active after compaction.</param>
    /// <param name="normalizedSummary">Validated summary text describing the compacted portion of the transcript.</param>
    /// <param name="reason">Reason the current compaction pass was triggered.</param>
    /// <param name="cancellationToken">Cancels artifact retrieval.</param>
    /// <returns>The compacted transcript state that should replace the original history.</returns>
    public async Task<CompactionResult> CreateResultAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> plannedMessagesToKeep,
        string normalizedSummary,
        CompactionReason reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalMessages);
        ArgumentNullException.ThrowIfNull(plannedMessagesToKeep);

        List<ChatMessage> preservedSystemMessages = [];
        List<ChatMessage> nonSystemMessages = [];
        foreach (ChatMessage message in originalMessages)
        {
            if (message.Role == ChatRole.System)
                preservedSystemMessages.Add(message);
            else
                nonSystemMessages.Add(message);
        }

        List<ChatMessage> messagesToKeep = [.. plannedMessagesToKeep];
        HashSet<ChatMessage> keptMessages = new(messagesToKeep);
        List<CompactionMessageReference> messageReferences = CreateMessageReferences(nonSystemMessages, keptMessages);
        List<CompactionMessageReference> archivedMessageReferences = CreateArchivedMessageReferences(nonSystemMessages, keptMessages);
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

    /// <summary>
    /// Creates a non-compacted result that passes the supplied messages through unchanged.
    /// </summary>
    /// <param name="messages">Messages that should remain as the active transcript.</param>
    /// <returns>A result whose compaction flag is disabled and whose tail is the original message list.</returns>
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

    /// <summary>
    /// Ensures each message carries a stable synthetic identity in its additional properties.
    /// </summary>
    /// <param name="messages">Messages that should be assigned identities before projection or persistence.</param>
    public static void EnsureMessageIdentities(List<ChatMessage> messages)
    {
        for (int index = 0; index < messages.Count; index++)
            _ = GetMessageIdentity(messages[index], index);
    }

    /// <summary>
    /// Creates the assistant summary artifact emitted by a compaction pass.
    /// </summary>
    /// <param name="summary">Normalized summary text.</param>
    /// <param name="reason">Reason the current compaction pass was triggered.</param>
    /// <param name="hasPreservedTail"><see langword="true" /> when non-system tail messages remain active after compaction.</param>
    /// <returns>An assistant message annotated as a compaction summary artifact.</returns>
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

    /// <summary>
    /// Creates the system boundary artifact that records how the preserved tail anchors the compacted transcript.
    /// </summary>
    /// <param name="originalMessages">Complete message history before compaction.</param>
    /// <param name="messageReferences">References describing the preserved non-system tail.</param>
    /// <param name="summary">Normalized summary text associated with the compaction pass.</param>
    /// <param name="reason">Reason the current compaction pass was triggered.</param>
    /// <returns>A system message annotated with boundary and anchor metadata.</returns>
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

    /// <summary>
    /// Creates references for the non-system messages that remain active after compaction.
    /// </summary>
    /// <param name="sourceMessages">All non-system messages in original transcript order.</param>
    /// <param name="keptMessages">Non-system messages selected for the preserved tail.</param>
    /// <returns>References describing the preserved non-system tail.</returns>
    private static List<CompactionMessageReference> CreateMessageReferences(
        List<ChatMessage> sourceMessages,
        ISet<ChatMessage> keptMessages)
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

    /// <summary>
    /// Creates references for the non-system messages archived into the summary portion of the compacted transcript.
    /// </summary>
    /// <param name="sourceMessages">All non-system messages in original transcript order.</param>
    /// <param name="keptMessages">Non-system messages selected for the preserved tail.</param>
    /// <returns>References describing the archived non-system messages.</returns>
    private static List<CompactionMessageReference> CreateArchivedMessageReferences(
        List<ChatMessage> sourceMessages,
        ISet<ChatMessage> keptMessages)
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

    /// <summary>
    /// Gets or assigns the synthetic identity used to correlate messages across compaction and collapse projections.
    /// </summary>
    /// <param name="message">Message whose identity should be read or generated.</param>
    /// <param name="index">Original message index used when generating a new identity.</param>
    /// <returns>The stable message identity stored in the message metadata.</returns>
    private static string GetMessageIdentity(ChatMessage message, int index)
    {
        message.AdditionalProperties ??= [];

        if (message.AdditionalProperties.TryGetValue(CompactionArtifactMetadata.MessageIdentityKey, out object? existingValue) &&
            existingValue is string existingId &&
            !string.IsNullOrWhiteSpace(existingId))
            return existingId;

        string generatedId = $"{index:D8}:{message.Role}:{Guid.CreateVersion7():N}";
        message.AdditionalProperties[CompactionArtifactMetadata.MessageIdentityKey] = generatedId;
        return generatedId;
    }
}
