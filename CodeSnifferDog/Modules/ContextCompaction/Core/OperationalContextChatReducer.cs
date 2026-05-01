using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class OperationalContextChatReducer : IChatReducer
{
    private const string SummaryOpenTag = "<summary>";
    private const string SummaryCloseTag = "</summary>";
    private readonly IOperationalContextCompactionArtifactsProvider? _artifactsProvider;
    private readonly IReadOnlyList<IOperationalContextCompactionCleanupHandler> _cleanupHandlers;
    private readonly IReadOnlyList<IOperationalContextCompactionHook> _hooks;
    private readonly OperationalContextCompactionOptions _options;
    private readonly IOperationalContextSummaryPromptProvider _summaryPromptProvider;
    private readonly IOperationalContextCompactionSummarizer _summarizer;

    public OperationalContextChatReducer(
        OperationalContextCompactionOptions options,
        IOperationalContextSummaryPromptProvider summaryPromptProvider,
        IOperationalContextCompactionSummarizer summarizer,
        IOperationalContextCompactionArtifactsProvider? artifactsProvider = null,
        IEnumerable<IOperationalContextCompactionHook>? hooks = null,
        IEnumerable<IOperationalContextCompactionCleanupHandler>? cleanupHandlers = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(summaryPromptProvider);
        ArgumentNullException.ThrowIfNull(summarizer);

        if (options.ModelContextWindowTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Model context window tokens must be greater than zero.");

        _artifactsProvider = artifactsProvider;
        _hooks = hooks?.ToArray() ?? [];
        _cleanupHandlers = cleanupHandlers?.ToArray() ?? [];
        _options = options;
        _summaryPromptProvider = summaryPromptProvider;
        _summarizer = summarizer;
    }

    public OperationalContextCompactionOptions Options => _options;

    public async Task<IEnumerable<ChatMessage>> ReduceAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        BuildMessages(await CompactAutomaticAsync(messages, cancellationToken).ConfigureAwait(false));

    public async Task<IEnumerable<ChatMessage>> ReduceReactiveAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        BuildMessages(await CompactReactiveAsync(messages, cancellationToken).ConfigureAwait(false));

    public async Task<OperationalContextCompactionResult> CompactAutomaticAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        await CompactCoreAsync(messages, OperationalContextCompactionReason.AutomaticThreshold, cancellationToken).ConfigureAwait(false);

    public async Task<OperationalContextCompactionResult> CompactReactiveAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        await CompactCoreAsync(messages, OperationalContextCompactionReason.Reactive, cancellationToken).ConfigureAwait(false);

    public static IReadOnlyList<ChatMessage> BuildMessages(OperationalContextCompactionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        List<ChatMessage> messages = [.. result.PreservedSystemMessages];

        if (!string.IsNullOrWhiteSpace(result.BoundaryMessage.Text))
            messages.Add(result.BoundaryMessage);

        if (!string.IsNullOrWhiteSpace(result.SummaryMessage.Text))
            messages.Add(result.SummaryMessage);

        if (!string.IsNullOrWhiteSpace(result.ContinuityStateMessage.Text))
            messages.Add(result.ContinuityStateMessage);

        messages.AddRange(result.MessagesToKeep);
        messages.AddRange(result.AttachmentMessages);
        messages.AddRange(result.HookResultMessages);

        return messages;
    }

    private async Task<OperationalContextCompactionResult> CompactCoreAsync(
        IEnumerable<ChatMessage> messages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        List<ChatMessage> materializedMessages = [.. messages];
        EnsureMessageIdentities(materializedMessages);
        if (!ShouldCompact(materializedMessages, reason))
            return CreatePassthroughResult(materializedMessages);

        await RunBeforeCompactionHooksAsync(materializedMessages, reason, cancellationToken).ConfigureAwait(false);

        string summaryPrompt = BuildSummaryPrompt(
            await _summaryPromptProvider.GetPromptAsync(cancellationToken).ConfigureAwait(false));

        if (string.IsNullOrWhiteSpace(summaryPrompt))
            throw new OperationalContextCompactionException("Operational context compaction summary prompt provider returned empty content.");

        try
        {
            string summary = await _summarizer.SummarizeAsync(materializedMessages, summaryPrompt, _options, cancellationToken).ConfigureAwait(false);
            summary = NormalizeSummary(summary);
            ValidateSummary(summary);

            OperationalContextCompactionResult result = await CreateResultAsync(materializedMessages, summary, reason, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ChatMessage> compactedMessages = BuildMessages(result);
            await RunAfterCompactionHooksAsync(materializedMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
            await RunCleanupHandlersAsync(materializedMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationalContextCompactionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new OperationalContextCompactionException("Operational context compaction summary generation failed.", ex);
        }
    }

    private async Task<OperationalContextCompactionResult> CreateResultAsync(
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
        OperationalContextCompactionArtifacts artifacts = _artifactsProvider is null
            ? OperationalContextCompactionArtifacts.Empty
            : await _artifactsProvider.GetArtifactsAsync(
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

    private void ValidateSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new OperationalContextCompactionException("Operational context compaction summary was empty.");

        foreach (string requiredFragment in _options.RequiredSummaryFragments)
        {
            if (string.IsNullOrWhiteSpace(requiredFragment))
                continue;

            if (!summary.Contains(requiredFragment, StringComparison.OrdinalIgnoreCase))
                throw new OperationalContextCompactionException(
                    $"Operational context compaction summary did not contain required fragment '{requiredFragment}'.");
        }
    }

    private static string BuildSummaryPrompt(string summaryPrompt) =>
        $"""
        {summaryPrompt}

        Summary contract:
        - Return text only.
        - Do not call tools.
        - Put your final answer inside a single {SummaryOpenTag}...{SummaryCloseTag} block.
        - The summary must retain enough continuity for the next agent turn to continue safely.
        - Do not output any content after the closing summary tag.
        """;

    private static string NormalizeSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new OperationalContextCompactionException("Operational context compaction summary was empty.");

        int openTagIndex = summary.IndexOf(SummaryOpenTag, StringComparison.OrdinalIgnoreCase);
        int closeTagIndex = summary.IndexOf(SummaryCloseTag, StringComparison.OrdinalIgnoreCase);

        if (openTagIndex < 0 || closeTagIndex < 0 || closeTagIndex <= openTagIndex)
            throw new OperationalContextCompactionException("Operational context compaction summary did not contain a valid <summary> block.");

        int contentStartIndex = openTagIndex + SummaryOpenTag.Length;
        string normalizedSummary = summary[contentStartIndex..closeTagIndex].Trim();

        if (string.IsNullOrWhiteSpace(normalizedSummary))
            throw new OperationalContextCompactionException("Operational context compaction summary <summary> block was empty.");

        return normalizedSummary;
    }

    private bool ShouldCompact(
        IReadOnlyList<ChatMessage> messages,
        OperationalContextCompactionReason reason)
    {
        if (reason == OperationalContextCompactionReason.Reactive)
            return true;

        int estimatedTokens = OperationalContextTokenEstimator.Estimate(messages);
        return estimatedTokens >= _options.GetAutoCompactThreshold();
    }

    private async ValueTask RunBeforeCompactionHooksAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (IOperationalContextCompactionHook hook in _hooks)
            await hook.OnBeforeCompactionAsync(originalMessages, reason, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RunAfterCompactionHooksAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (IOperationalContextCompactionHook hook in _hooks)
            await hook.OnAfterCompactionAsync(originalMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RunCleanupHandlersAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        foreach (IOperationalContextCompactionCleanupHandler cleanupHandler in _cleanupHandlers)
            await cleanupHandler.CleanupAsync(originalMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
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
            int messageTokens = OperationalContextTokenEstimator.Estimate([message]);

            if (keptMessages.Count > 0 && totalTokens >= _options.PreservedTailMaxTokens)
                break;

            keptMessages.Insert(0, message);
            totalTokens += messageTokens;
            messageCount++;

            bool reachedMinimumTail =
                totalTokens >= _options.PreservedTailMinTokens &&
                messageCount >= _options.PreservedTailMinMessages;

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

    private static OperationalContextCompactionResult CreatePassthroughResult(IReadOnlyList<ChatMessage> messages) => new()
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

    private static void EnsureMessageIdentities(List<ChatMessage> messages)
    {
        for (int index = 0; index < messages.Count; index++)
            _ = GetMessageIdentity(messages[index], index);
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
