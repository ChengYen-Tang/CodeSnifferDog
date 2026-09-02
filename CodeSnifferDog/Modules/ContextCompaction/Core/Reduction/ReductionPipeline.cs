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
/// <param name="planner">Planner that decides whether compaction is needed and selects the preserved tail.</param>
internal sealed class ReductionPipeline(
    CompactionOptions options,
    ISummaryPromptProvider summaryPromptProvider,
    ISummarizer summarizer,
    ICompactionArtifactsProvider? artifactsProvider,
    IEnumerable<IHook>? hooks,
    IEnumerable<ICleanupHandler>? cleanupHandlers,
    ICompactionPlanner? planner = null)
{
    private readonly CompactionHookDispatcher _hookDispatcher = new(hooks, cleanupHandlers);
    private readonly ICompactionPlanner _planner = planner ?? new LegacyCompactionPlanner(options);
    private readonly CompactionResultBuilder _resultBuilder = new(artifactsProvider);

    /// <summary>
    /// Compacts using transcript-only token estimation.
    /// </summary>
    /// <param name="messages">Transcript messages to compact.</param>
    /// <param name="reason">Reason that triggered the compaction evaluation.</param>
    /// <param name="cancellationToken">Cancels the compaction attempt.</param>
    /// <returns>The resulting compaction result.</returns>
    public Task<CompactionResult> CompactAsync(
        IEnumerable<ChatMessage> messages,
        CompactionReason reason,
        CancellationToken cancellationToken) =>
        CompactAsync(messages, reason, inputTokenAdjustmentTokens: 0, cancellationToken: cancellationToken);

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
        int inputTokenAdjustmentTokens,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        List<ChatMessage> materializedMessages = [.. messages];
        CompactionResultBuilder.EnsureMessageIdentities(materializedMessages);

        // A provider cannot accept a summary request or compacted context that splits a function call from its results.
        // Defer compaction until the function-invocation loop has appended the complete result sequence.
        if (!ToolCallTranscript.IsComplete(materializedMessages))
            return CompactionResultBuilder.CreatePassthroughResult(materializedMessages);

        CompactionPlan plan = await _planner
            .PlanAsync(materializedMessages, reason, inputTokenAdjustmentTokens, cancellationToken)
            .ConfigureAwait(false);
        if (!plan.ShouldCompact)
            return CompactionResultBuilder.CreatePassthroughResult(materializedMessages);

        await _hookDispatcher.RunBeforeCompactionAsync(materializedMessages, reason, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ChatMessage> messagesToSummarize = GetMessagesToSummarize(
            materializedMessages,
            plan.MessagesToKeep);

        string summaryPrompt = SummaryContract.BuildPrompt(
            await summaryPromptProvider.GetPromptAsync(cancellationToken).ConfigureAwait(false),
            options.RequiredSummaryFragments);

        if (string.IsNullOrWhiteSpace(summaryPrompt))
            throw new CompactionException("Operational context compaction summary prompt provider returned empty content.");

        try
        {
            string summary = await summarizer.SummarizeAsync(messagesToSummarize, summaryPrompt, options, cancellationToken).ConfigureAwait(false);
            summary = SummaryContract.Normalize(summary);
            SummaryContract.Validate(summary, options);

            CompactionResult result = await _resultBuilder
                .CreateResultAsync(
                    materializedMessages,
                    plan.MessagesToKeep,
                    summary,
                    reason,
                    cancellationToken)
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
    /// Excludes the retained tail from the summary request. The tail remains in the provider context after compaction,
    /// so including it in both places only makes the summary call larger and can prevent the recovery from succeeding.
    /// </summary>
    private static IReadOnlyList<ChatMessage> GetMessagesToSummarize(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatMessage> messagesToKeep)
    {
        if (messagesToKeep.Count == 0)
            return messages;

        int latestCandidateStart = messages.Count - messagesToKeep.Count;
        for (int start = 0; start <= latestCandidateStart; start++)
        {
            bool matches = true;
            for (int offset = 0; offset < messagesToKeep.Count; offset++)
            {
                if (ReferenceEquals(messages[start + offset], messagesToKeep[offset]))
                    continue;

                matches = false;
                break;
            }

            if (matches)
                return [.. messages.Take(start)];
        }

        // Custom planners may return cloned messages. Preserve existing behavior in that case rather than risk
        // omitting transcript content from the summary.
        return messages;
    }

}
