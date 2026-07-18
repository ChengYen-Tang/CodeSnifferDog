using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Transcript;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

/// <summary>
/// Runs the end-to-end compaction pipeline: threshold decision, hooks, summary generation, validation, and result building.
/// </summary>
/// <param name="options">Compaction settings that control thresholds and summary validation rules.</param>
/// <param name="summaryPromptProvider">Provider for the summary prompt template.</param>
/// <param name="summarizer">Summarizer that generates the raw summary text.</param>
/// <param name="artifactsProvider">Optional provider that reattaches artifact messages after compaction.</param>
/// <param name="hooks">Optional hooks that run before and after compaction.</param>
/// <param name="cleanupHandlers">Optional cleanup handlers that run after successful compaction.</param>
internal sealed class ReductionPipeline(
    CompactionOptions options,
    ISummaryPromptProvider summaryPromptProvider,
    ISummarizer summarizer,
    ICompactionArtifactsProvider? artifactsProvider,
    IEnumerable<IHook>? hooks,
    IEnumerable<ICleanupHandler>? cleanupHandlers)
{
    private readonly CompactionHookDispatcher _hookDispatcher = new(hooks, cleanupHandlers);
    private readonly CompactionResultBuilder _resultBuilder = new(options, artifactsProvider);

    /// <summary>
    /// Compacts the supplied transcript when the selected reason or thresholds require it.
    /// </summary>
    /// <param name="messages">Transcript messages to compact.</param>
    /// <param name="reason">Reason that triggered this compaction pass.</param>
    /// <param name="cancellationToken">Cancels the compaction attempt.</param>
    /// <returns>A pass-through result when compaction is not needed; otherwise the fully built compaction result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="messages" /> is <see langword="null" />.</exception>
    /// <exception cref="CompactionException">Summary prompt retrieval, summary generation, or summary validation fails.</exception>
    public async Task<CompactionResult> CompactAsync(
        IEnumerable<ChatMessage> messages,
        CompactionReason reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        List<ChatMessage> materializedMessages = [.. messages];
        CompactionResultBuilder.EnsureMessageIdentities(materializedMessages);
        if (!ShouldCompact(materializedMessages, reason))
            return CompactionResultBuilder.CreatePassthroughResult(materializedMessages);

        // A provider cannot accept a summary request or compacted context that splits a function call from its results.
        // Defer compaction until the function-invocation loop has appended the complete result sequence.
        if (!ToolCallTranscript.IsComplete(materializedMessages))
            return CompactionResultBuilder.CreatePassthroughResult(materializedMessages);

        await _hookDispatcher.RunBeforeCompactionAsync(materializedMessages, reason, cancellationToken).ConfigureAwait(false);

        string summaryPrompt = SummaryContract.BuildPrompt(
            await summaryPromptProvider.GetPromptAsync(cancellationToken).ConfigureAwait(false),
            options.RequiredSummaryFragments);

        if (string.IsNullOrWhiteSpace(summaryPrompt))
            throw new CompactionException("Operational context compaction summary prompt provider returned empty content.");

        try
        {
            string summary = await summarizer.SummarizeAsync(materializedMessages, summaryPrompt, options, cancellationToken).ConfigureAwait(false);
            summary = SummaryContract.Normalize(summary);
            SummaryContract.Validate(summary, options);

            CompactionResult result = await _resultBuilder
                .CreateResultAsync(materializedMessages, summary, reason, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<ChatMessage> compactedMessages = CompactionMessageBuilder.Build(result);
            await _hookDispatcher.RunAfterCompactionAsync(materializedMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
            await _hookDispatcher.RunCleanupAsync(materializedMessages, compactedMessages, reason, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (CompactionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CompactionException("Operational context compaction summary generation failed.", ex);
        }
    }

    /// <summary>
    /// Determines whether the transcript should be compacted for the current reason.
    /// </summary>
    /// <param name="messages">Materialized transcript messages with stable identities.</param>
    /// <param name="reason">Reason that triggered the compaction evaluation.</param>
    /// <returns><see langword="true" /> for reactive compaction, or when the automatic threshold is exceeded.</returns>
    private bool ShouldCompact(
        IReadOnlyList<ChatMessage> messages,
        CompactionReason reason)
    {
        if (reason == CompactionReason.Reactive)
            return true;

        int estimatedTokens = TokenEstimator.Estimate(messages);
        return estimatedTokens >= options.GetAutoCompactThreshold();
    }
}
