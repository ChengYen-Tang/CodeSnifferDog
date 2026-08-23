using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

/// <summary>
/// Coordinates summary generation and transcript rewriting for automatic or reactive compaction.
/// </summary>
public sealed class ChatReducer : IChatReducer
{
    private readonly CompactionOptions _options;
    private readonly ReductionPipeline _pipeline;

    /// <summary>
    /// Creates a reducer that can summarize history and rebuild the compacted transcript.
    /// </summary>
    /// <param name="options">Compaction settings that define thresholds, token budgets, and retained tail limits.</param>
    /// <param name="summaryPromptProvider">Provider for the summary prompt contract.</param>
    /// <param name="summarizer">Component that generates the normalized summary text.</param>
    /// <param name="artifactsProvider">Optional provider that reattaches artifact messages after compaction.</param>
    /// <param name="hooks">Optional hooks that can observe or enrich a compaction pass.</param>
    /// <param name="cleanupHandlers">Optional handlers that clean up side effects after compaction finishes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options" />, <paramref name="summaryPromptProvider" />, or <paramref name="summarizer" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options" /> declares a non-positive model context window.</exception>
    public ChatReducer(
        CompactionOptions options,
        ISummaryPromptProvider summaryPromptProvider,
        ISummarizer summarizer,
        ICompactionArtifactsProvider? artifactsProvider = null,
        IEnumerable<IHook>? hooks = null,
        IEnumerable<ICleanupHandler>? cleanupHandlers = null) : this(
            options,
            summaryPromptProvider,
            summarizer,
            artifactsProvider,
            hooks,
            cleanupHandlers,
            new LegacyCompactionPlanner(options))
    {
    }

    internal ChatReducer(
        CompactionOptions options,
        ISummaryPromptProvider summaryPromptProvider,
        ISummarizer summarizer,
        ICompactionArtifactsProvider? artifactsProvider,
        IEnumerable<IHook>? hooks,
        IEnumerable<ICleanupHandler>? cleanupHandlers,
        ICompactionPlanner planner)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(summaryPromptProvider);
        ArgumentNullException.ThrowIfNull(summarizer);
        ArgumentNullException.ThrowIfNull(planner);

        if (options.ModelContextWindowTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Model context window tokens must be greater than zero.");

        _options = options;
        _pipeline = new ReductionPipeline(
            options,
            summaryPromptProvider,
            summarizer,
            artifactsProvider,
            hooks,
            cleanupHandlers,
            planner);
    }

    /// <summary>
    /// Gets the active compaction settings used by this reducer.
    /// </summary>
    public CompactionOptions Options => _options;

    /// <summary>
    /// Performs threshold-driven compaction and returns the resulting active transcript messages.
    /// </summary>
    /// <param name="messages">Transcript messages to evaluate for automatic compaction.</param>
    /// <param name="cancellationToken">Cancels the compaction attempt.</param>
    /// <returns>The message sequence that should remain active after the automatic pass.</returns>
    public async Task<IEnumerable<ChatMessage>> ReduceAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        BuildMessages(await CompactAutomaticAsync(messages, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Performs reactive compaction and returns the resulting active transcript messages.
    /// </summary>
    /// <param name="messages">Transcript messages to compact in response to a retry or explicit collapse trigger.</param>
    /// <param name="cancellationToken">Cancels the compaction attempt.</param>
    /// <returns>The message sequence that should remain active after the reactive pass.</returns>
    public async Task<IEnumerable<ChatMessage>> ReduceReactiveAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        BuildMessages(await CompactReactiveAsync(messages, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Runs the automatic compaction pipeline using the automatic-threshold reason code.
    /// </summary>
    /// <param name="messages">Transcript messages to evaluate for automatic compaction.</param>
    /// <param name="cancellationToken">Cancels the compaction attempt.</param>
    /// <returns>The detailed compaction result for the automatic pass.</returns>
    public async Task<CompactionResult> CompactAutomaticAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        await CompactAutomaticAsync(messages, additionalEstimatedInputTokens: 0, cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Runs automatic compaction with request-level input tokens that are not represented by transcript messages.
    /// </summary>
    /// <param name="messages">Transcript messages to evaluate for automatic compaction.</param>
    /// <param name="additionalEstimatedInputTokens">A non-negative provider-request overhead estimate.</param>
    /// <param name="cancellationToken">Cancels the compaction attempt.</param>
    /// <returns>The detailed compaction result for the automatic pass.</returns>
    public async Task<CompactionResult> CompactAutomaticAsync(
        IEnumerable<ChatMessage> messages,
        int additionalEstimatedInputTokens,
        CancellationToken cancellationToken = default) =>
        await _pipeline
            .CompactAsync(messages, CompactionReason.AutomaticThreshold, additionalEstimatedInputTokens, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Runs the compaction pipeline using the reactive reason code.
    /// </summary>
    /// <param name="messages">Transcript messages to compact reactively.</param>
    /// <param name="cancellationToken">Cancels the compaction attempt.</param>
    /// <returns>The detailed compaction result for the reactive pass.</returns>
    public async Task<CompactionResult> CompactReactiveAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        await _pipeline
            .CompactAsync(messages, CompactionReason.Reactive, additionalEstimatedInputTokens: 0, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Rebuilds the active transcript from a previously computed compaction result.
    /// </summary>
    /// <param name="result">Compaction result containing preserved system messages, summary messages, and kept tail messages.</param>
    /// <returns>The ordered message list that should replace the original transcript.</returns>
    public static IReadOnlyList<ChatMessage> BuildMessages(CompactionResult result) =>
        CompactionMessageBuilder.Build(result);
}
