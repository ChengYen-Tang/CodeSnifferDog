using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using System.Globalization;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class OperationalContextChatReducer : IChatReducer
{
    private readonly IReadOnlyList<IOperationalContextCompactionArtifactProvider> _artifactProviders;
    private const string SummaryOpenTag = "<summary>";
    private const string SummaryCloseTag = "</summary>";
    private readonly IReadOnlyList<IOperationalContextCompactionCleanupHandler> _cleanupHandlers;
    private readonly IReadOnlyList<IOperationalContextCompactionHook> _hooks;
    private readonly OperationalContextCompactionOptions _options;
    private readonly IOperationalContextSummaryPromptProvider _summaryPromptProvider;
    private readonly IOperationalContextCompactionSummarizer _summarizer;
    private readonly IOperationalContextCompactionUsageProvider _usageProvider;
    private int _automaticConsecutiveFailureCount;
    private int _reactiveConsecutiveFailureCount;

    public OperationalContextChatReducer(
        OperationalContextCompactionOptions options,
        IOperationalContextSummaryPromptProvider summaryPromptProvider,
        IOperationalContextCompactionSummarizer summarizer,
        IOperationalContextCompactionUsageProvider usageProvider,
        IEnumerable<IOperationalContextCompactionArtifactProvider>? artifactProviders = null,
        IEnumerable<IOperationalContextCompactionHook>? hooks = null,
        IEnumerable<IOperationalContextCompactionCleanupHandler>? cleanupHandlers = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(summaryPromptProvider);
        ArgumentNullException.ThrowIfNull(summarizer);
        ArgumentNullException.ThrowIfNull(usageProvider);

        if (options.ContextTokenThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Context token threshold must be greater than zero.");

        if (options.ContextWindowBufferTokens < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Context window buffer tokens cannot be negative.");

        if (options.SummaryReservedOutputTokens < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Summary reserved output tokens cannot be negative.");

        if (options.PreservedTailMessageCount < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Preserved tail message count cannot be negative.");

        if (options.MaxConsecutiveFailures <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Max consecutive failures must be greater than zero.");

        _artifactProviders = artifactProviders?.ToArray() ?? [];
        _hooks = hooks?.ToArray() ?? [];
        _cleanupHandlers = cleanupHandlers?.ToArray() ?? [];
        _options = options;
        _summaryPromptProvider = summaryPromptProvider;
        _summarizer = summarizer;
        _usageProvider = usageProvider;
    }

    public async Task<IEnumerable<ChatMessage>> ReduceAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        await ReduceCoreAsync(messages, OperationalContextCompactionReason.AutomaticThreshold, cancellationToken).ConfigureAwait(false);

    public async Task<IEnumerable<ChatMessage>> ReduceReactiveAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        await ReduceCoreAsync(messages, OperationalContextCompactionReason.Reactive, cancellationToken).ConfigureAwait(false);

    private async Task<IEnumerable<ChatMessage>> ReduceCoreAsync(
        IEnumerable<ChatMessage> messages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        List<ChatMessage> materializedMessages = [.. messages];

        if (reason == OperationalContextCompactionReason.AutomaticThreshold &&
            GetConsecutiveFailureCount(reason) >= _options.MaxConsecutiveFailures)
            return materializedMessages;

        OperationalContextCompactionUsage? usage = await _usageProvider.GetUsageAsync(materializedMessages, cancellationToken).ConfigureAwait(false);

        if (!ShouldCompact(usage, reason))
            return materializedMessages;

        await RunBeforeCompactionHooksAsync(materializedMessages, reason, cancellationToken).ConfigureAwait(false);

        string summaryPrompt = BuildSummaryPrompt(
            await _summaryPromptProvider.GetPromptAsync(materializedMessages, cancellationToken).ConfigureAwait(false));

        if (string.IsNullOrWhiteSpace(summaryPrompt))
            throw new OperationalContextCompactionException("Operational context compaction summary prompt provider returned empty content.");

        string summary;

        try
        {
            summary = await _summarizer.SummarizeAsync(materializedMessages, summaryPrompt, _options, cancellationToken).ConfigureAwait(false);
            summary = NormalizeSummary(summary);
            ResetFailureCount(reason);
        }
        catch (OperationalContextCompactionException)
        {
            if (RegisterFailureAndCheckCircuitBreaker(reason))
                return materializedMessages;

            throw;
        }
        catch (Exception ex)
        {
            if (RegisterFailureAndCheckCircuitBreaker(reason))
                return materializedMessages;

            throw new OperationalContextCompactionException("Operational context compaction summary generation failed.", ex);
        }

        ValidateSummary(summary);

        OperationalContextCompactionResult compactionResult = await BuildCompactionResultAsync(
            materializedMessages,
            summary,
            reason,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ChatMessage> compactedMessages = AssembleCompactedMessages(compactionResult);
        await RunAfterCompactionHooksAsync(materializedMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
        await RunCleanupHandlersAsync(materializedMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);

        return compactedMessages;
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
        - Put the final operational summary inside a single {SummaryOpenTag}...{SummaryCloseTag} block.
        - The summary must retain enough detail for the next agent turn to continue work safely.
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

    private int GetConsecutiveFailureCount(OperationalContextCompactionReason reason) =>
        reason == OperationalContextCompactionReason.AutomaticThreshold
            ? Interlocked.CompareExchange(ref _automaticConsecutiveFailureCount, 0, 0)
            : Interlocked.CompareExchange(ref _reactiveConsecutiveFailureCount, 0, 0);

    private bool RegisterFailureAndCheckCircuitBreaker(OperationalContextCompactionReason reason) =>
        reason == OperationalContextCompactionReason.AutomaticThreshold
            ? Interlocked.Increment(ref _automaticConsecutiveFailureCount) >= _options.MaxConsecutiveFailures
            : Interlocked.Increment(ref _reactiveConsecutiveFailureCount) >= _options.MaxConsecutiveFailures;

    private void ResetFailureCount(OperationalContextCompactionReason reason)
    {
        if (reason == OperationalContextCompactionReason.AutomaticThreshold)
            Interlocked.Exchange(ref _automaticConsecutiveFailureCount, 0);
        else
            Interlocked.Exchange(ref _reactiveConsecutiveFailureCount, 0);
    }

    private bool ShouldCompact(
        OperationalContextCompactionUsage? usage,
        OperationalContextCompactionReason reason)
    {
        if (reason == OperationalContextCompactionReason.Reactive)
            return true;

        if (usage is null)
            return false;

        if (usage.ContextWindowTokens is long contextWindowTokens)
        {
            long triggerThreshold = Math.Max(
                1,
                contextWindowTokens - _options.ContextWindowBufferTokens - _options.SummaryReservedOutputTokens);

            return usage.UsedTokens >= triggerThreshold;
        }

        return usage.UsedTokens >= _options.ContextTokenThreshold;
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

    private async ValueTask<OperationalContextCompactionResult> BuildCompactionResultAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        string summary,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChatMessage> preservedSystemMessages = _options.PreserveSystemMessages
            ? [.. originalMessages.Where(static message => message.Role == ChatRole.System)]
            : [];
        IReadOnlyList<ChatMessage> candidateMessages = _options.PreserveSystemMessages
            ? [.. originalMessages.Where(static message => message.Role != ChatRole.System)]
            : originalMessages;
        (ChatMessage? boundaryAnchorMessage, IReadOnlyList<ChatMessage> preservedTailMessages) =
            SelectBoundaryAnchorAndTailMessages(candidateMessages);
        IReadOnlyList<OperationalContextCompactionMessageReference> messageReferences =
            [.. preservedTailMessages.Select(message => CreateMessageReference(originalMessages, message))];
        OperationalContextCompactionArtifacts artifacts =
            await CreateArtifactsAsync(originalMessages, preservedTailMessages, reason, cancellationToken).ConfigureAwait(false);

        return new OperationalContextCompactionResult
        {
            PreservedSystemMessages = preservedSystemMessages,
            BoundaryMessage = CreateBoundaryMessage(originalMessages, boundaryAnchorMessage, preservedTailMessages, reason),
            SummaryMessage = CreateSummaryMessage(
                summary,
                reason,
                messageReferences,
                artifacts.AttachmentMessages.Count,
                artifacts.HookResultMessages.Count),
            MessagesToKeep = preservedTailMessages,
            MessageReferences = messageReferences,
            AttachmentMessages = artifacts.AttachmentMessages,
            HookResultMessages = artifacts.HookResultMessages,
        };
    }

    private static IReadOnlyList<ChatMessage> AssembleCompactedMessages(OperationalContextCompactionResult compactionResult)
    {
        List<ChatMessage> compactedMessages =
        [
            .. compactionResult.PreservedSystemMessages,
            compactionResult.BoundaryMessage,
            compactionResult.SummaryMessage,
            .. compactionResult.MessagesToKeep,
            .. compactionResult.AttachmentMessages,
            .. compactionResult.HookResultMessages,
        ];

        return compactedMessages;
    }

    private (ChatMessage? BoundaryAnchorMessage, IReadOnlyList<ChatMessage> PreservedTailMessages)
        SelectBoundaryAnchorAndTailMessages(IReadOnlyList<ChatMessage> candidateMessages)
    {
        if (candidateMessages.Count == 0)
            return (null, []);

        int preservedTailCount = Math.Min(_options.PreservedTailMessageCount, candidateMessages.Count);
        int tailStartIndex = candidateMessages.Count - preservedTailCount;
        ChatMessage? boundaryAnchorMessage = tailStartIndex > 0 ? candidateMessages[tailStartIndex - 1] : null;

        return (boundaryAnchorMessage, [.. candidateMessages.Skip(tailStartIndex).Take(preservedTailCount)]);
    }

    private ChatMessage CreateBoundaryMessage(
        IReadOnlyList<ChatMessage> originalMessages,
        ChatMessage? boundaryAnchorMessage,
        IReadOnlyList<ChatMessage> preservedTailMessages,
        OperationalContextCompactionReason reason)
    {
        OperationalContextCompactionMessageReference? anchorReference = boundaryAnchorMessage is not null
            ? CreateMessageReference(originalMessages, boundaryAnchorMessage)
            : null;
        IReadOnlyList<OperationalContextCompactionMessageReference> tailReferences =
            [.. preservedTailMessages.Select(message => CreateMessageReference(originalMessages, message))];
        ChatMessage boundaryMessage = new(
            ChatRole.System,
            "Operational context boundary marker");

        boundaryMessage.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            [OperationalContextCompactionArtifactMetadata.ArtifactKindKey] = OperationalContextCompactionArtifactMetadata.BoundaryArtifactKind,
            [OperationalContextCompactionArtifactMetadata.CompactionReasonKey] = reason.ToString(),
            [OperationalContextCompactionArtifactMetadata.PreservedTailCountKey] = preservedTailMessages.Count,
            [OperationalContextCompactionArtifactMetadata.BoundarySummaryKey] =
                anchorReference is null
                    ? "No preserved boundary anchor from prior conversation."
                    : "Anchored to the message immediately preceding the preserved tail segment.",
            [OperationalContextCompactionArtifactMetadata.PreservedTailIndexesKey] =
                string.Join(",", tailReferences.Select(static reference => reference.MessageIndex.ToString(CultureInfo.InvariantCulture))),
            [OperationalContextCompactionArtifactMetadata.PreservedTailIdsKey] =
                string.Join(",", tailReferences.Select(static reference => reference.MessageId ?? string.Empty)),
            [OperationalContextCompactionArtifactMetadata.PreservedTailTextsKey] =
                string.Join(" | ", preservedTailMessages.Select(static message => message.Text ?? string.Empty)),
        };
        AddPreservedSegmentReferences(boundaryMessage.AdditionalProperties, tailReferences);

        if (anchorReference is not null)
        {
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorIndexKey] = anchorReference.MessageIndex;
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorIdKey] = anchorReference.MessageId ?? string.Empty;
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorRoleKey] = anchorReference.Role.Value;
            boundaryMessage.AdditionalProperties[OperationalContextCompactionArtifactMetadata.BoundaryAnchorTextKey] = anchorReference.Text;
        }

        return boundaryMessage;
    }

    private ChatMessage CreateSummaryMessage(
        string summary,
        OperationalContextCompactionReason reason,
        IReadOnlyList<OperationalContextCompactionMessageReference> messageReferences,
        int attachmentCount,
        int hookResultCount)
    {
        ChatMessage summaryMessage = new(
            _options.SummaryMessageRole,
            $"{_options.SummaryMessageHeader}{Environment.NewLine}{Environment.NewLine}{summary}");

        summaryMessage.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            [OperationalContextCompactionArtifactMetadata.ArtifactKindKey] = OperationalContextCompactionArtifactMetadata.SummaryArtifactKind,
            [OperationalContextCompactionArtifactMetadata.CompactionReasonKey] = reason.ToString(),
            [OperationalContextCompactionArtifactMetadata.SummaryFormatVersionKey] = OperationalContextCompactionArtifactMetadata.CurrentSummaryFormatVersion,
            [OperationalContextCompactionArtifactMetadata.IsCompactionSummaryKey] = true,
            [OperationalContextCompactionArtifactMetadata.HasPreservedTailKey] = messageReferences.Count > 0,
            [OperationalContextCompactionArtifactMetadata.MessagesToKeepCountKey] = messageReferences.Count,
            [OperationalContextCompactionArtifactMetadata.AttachmentsCountKey] = attachmentCount,
            [OperationalContextCompactionArtifactMetadata.HookResultsCountKey] = hookResultCount,
        };

        return summaryMessage;
    }

    private async ValueTask<OperationalContextCompactionArtifacts> CreateArtifactsAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> messagesToKeep,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken)
    {
        if (_artifactProviders.Count == 0)
            return OperationalContextCompactionArtifacts.Empty;

        List<ChatMessage> attachmentMessages = [];
        List<ChatMessage> hookResultMessages = [];

        foreach (IOperationalContextCompactionArtifactProvider artifactProvider in _artifactProviders)
        {
            OperationalContextCompactionArtifacts artifacts =
                await artifactProvider.CreateArtifactsAsync(originalMessages, messagesToKeep, reason, cancellationToken).ConfigureAwait(false);

            attachmentMessages.AddRange(artifacts.AttachmentMessages);
            hookResultMessages.AddRange(artifacts.HookResultMessages);
        }

        return new OperationalContextCompactionArtifacts
        {
            AttachmentMessages = attachmentMessages,
            HookResultMessages = hookResultMessages,
        };
    }

    private static void AddPreservedSegmentReferences(
        AdditionalPropertiesDictionary metadata,
        IReadOnlyList<OperationalContextCompactionMessageReference> tailReferences)
    {
        if (tailReferences.FirstOrDefault() is not OperationalContextCompactionMessageReference headReference ||
            tailReferences.LastOrDefault() is not OperationalContextCompactionMessageReference tailReference)
            return;

        metadata[OperationalContextCompactionArtifactMetadata.PreservedSegmentHeadIndexKey] = headReference.MessageIndex;
        metadata[OperationalContextCompactionArtifactMetadata.PreservedSegmentHeadIdKey] = headReference.MessageId ?? string.Empty;
        metadata[OperationalContextCompactionArtifactMetadata.PreservedSegmentTailIndexKey] = tailReference.MessageIndex;
        metadata[OperationalContextCompactionArtifactMetadata.PreservedSegmentTailIdKey] = tailReference.MessageId ?? string.Empty;
    }

    private static OperationalContextCompactionMessageReference CreateMessageReference(
        IReadOnlyList<ChatMessage> originalMessages,
        ChatMessage message)
    {
        int messageIndex = FindMessageIndex(originalMessages, message);

        if (messageIndex < 0)
            throw new OperationalContextCompactionException("Failed to map a preserved message back to the original conversation.");

        return new OperationalContextCompactionMessageReference
        {
            MessageIndex = messageIndex,
            MessageId = GetMessageId(message),
            Role = message.Role,
            Text = message.Text ?? string.Empty,
        };
    }

    private static int FindMessageIndex(IReadOnlyList<ChatMessage> messages, ChatMessage target)
    {
        for (int index = 0; index < messages.Count; index++)
            if (ReferenceEquals(messages[index], target))
                return index;

        return -1;
    }

    private static string? GetMessageId(ChatMessage message)
    {
        if (message.AdditionalProperties is null)
            return null;

        if (TryGetAdditionalProperty(message.AdditionalProperties, "message_id", out string? messageId))
            return messageId;

        if (TryGetAdditionalProperty(message.AdditionalProperties, "id", out messageId))
            return messageId;

        return null;
    }

    private static bool TryGetAdditionalProperty(
        AdditionalPropertiesDictionary additionalProperties,
        string key,
        out string? value)
    {
        value = null;

        if (!additionalProperties.TryGetValue(key, out object? rawValue) || rawValue is null)
            return false;

        value = rawValue.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }
}
