using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class ChatReducer : IChatReducer
{
    private readonly CompactionOptions _options;
    private readonly ReductionPipeline _pipeline;

    public ChatReducer(
        CompactionOptions options,
        ISummaryPromptProvider summaryPromptProvider,
        ISummarizer summarizer,
        ICompactionArtifactsProvider? artifactsProvider = null,
        IEnumerable<IHook>? hooks = null,
        IEnumerable<ICleanupHandler>? cleanupHandlers = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(summaryPromptProvider);
        ArgumentNullException.ThrowIfNull(summarizer);

        if (options.ModelContextWindowTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Model context window tokens must be greater than zero.");

        _options = options;
        _pipeline = new ReductionPipeline(
            options,
            summaryPromptProvider,
            summarizer,
            artifactsProvider,
            hooks,
            cleanupHandlers);
    }

    public CompactionOptions Options => _options;

    public async Task<IEnumerable<ChatMessage>> ReduceAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        BuildMessages(await CompactAutomaticAsync(messages, cancellationToken).ConfigureAwait(false));

    public async Task<IEnumerable<ChatMessage>> ReduceReactiveAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        BuildMessages(await CompactReactiveAsync(messages, cancellationToken).ConfigureAwait(false));

    public async Task<CompactionResult> CompactAutomaticAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        await _pipeline.CompactAsync(messages, CompactionReason.AutomaticThreshold, cancellationToken).ConfigureAwait(false);

    public async Task<CompactionResult> CompactReactiveAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        await _pipeline.CompactAsync(messages, CompactionReason.Reactive, cancellationToken).ConfigureAwait(false);

    public static IReadOnlyList<ChatMessage> BuildMessages(CompactionResult result) =>
        CompactionMessageBuilder.Build(result);
}
