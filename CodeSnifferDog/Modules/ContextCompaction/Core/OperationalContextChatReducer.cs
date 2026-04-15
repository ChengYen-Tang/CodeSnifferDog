using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using System.Globalization;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class OperationalContextChatReducer : IChatReducer
{
    private const string SummaryOpenTag = "<summary>";
    private const string SummaryCloseTag = "</summary>";
    private readonly IReadOnlyList<IOperationalContextCompactionCleanupHandler> _cleanupHandlers;
    private readonly IReadOnlyList<IOperationalContextCompactionHook> _hooks;
    private readonly OperationalContextCompactionOptions _options;
    private readonly IOperationalContextSummaryPromptProvider _summaryPromptProvider;
    private readonly IOperationalContextCompactionSummarizer _summarizer;
    private readonly IOperationalContextCompactionUsageProvider _usageProvider;

    public OperationalContextChatReducer(
        OperationalContextCompactionOptions options,
        IOperationalContextSummaryPromptProvider summaryPromptProvider,
        IOperationalContextCompactionSummarizer summarizer,
        IOperationalContextCompactionUsageProvider usageProvider,
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

        OperationalContextCompactionUsage? usage = await _usageProvider.GetUsageAsync(materializedMessages, cancellationToken).ConfigureAwait(false);

        if (!ShouldCompact(usage, reason))
            return materializedMessages;

        await RunBeforeCompactionHooksAsync(materializedMessages, reason, cancellationToken).ConfigureAwait(false);

        string summaryPrompt = BuildSummaryPrompt(
            await _summaryPromptProvider.GetPromptAsync(materializedMessages, cancellationToken).ConfigureAwait(false));

        if (string.IsNullOrWhiteSpace(summaryPrompt))
            throw new OperationalContextCompactionException("Operational context compaction summary prompt provider returned empty content.");

        try
        {
            string summary = await _summarizer.SummarizeAsync(materializedMessages, summaryPrompt, _options, cancellationToken).ConfigureAwait(false);
            summary = NormalizeSummary(summary);
            ValidateSummary(summary);
            IReadOnlyList<ChatMessage> compactedMessages =
            [
                CreateSummaryMessage(summary, reason),
            ];
            await RunAfterCompactionHooksAsync(materializedMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
            await RunCleanupHandlersAsync(materializedMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
            return compactedMessages;
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

    private static ChatMessage CreateSummaryMessage(string summary, OperationalContextCompactionReason reason)
    {
        ChatMessage summaryMessage = new(
            ChatRole.Assistant,
            $"Operational summary checkpoint{Environment.NewLine}{Environment.NewLine}{summary}");

        summaryMessage.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            [OperationalContextCompactionArtifactMetadata.ArtifactKindKey] = OperationalContextCompactionArtifactMetadata.SummaryArtifactKind,
            [OperationalContextCompactionArtifactMetadata.CompactionReasonKey] = reason.ToString(),
            [OperationalContextCompactionArtifactMetadata.SummaryFormatVersionKey] = OperationalContextCompactionArtifactMetadata.CurrentSummaryFormatVersion,
            [OperationalContextCompactionArtifactMetadata.IsCompactionSummaryKey] = true,
        };

        return summaryMessage;
    }
}
