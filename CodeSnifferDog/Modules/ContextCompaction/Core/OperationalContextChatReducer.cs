using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class OperationalContextChatReducer : IChatReducer
{
    private readonly OperationalContextCompactionOptions _options;
    private readonly ReductionPipeline _pipeline;

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

        _options = options;
        _pipeline = new ReductionPipeline(
            options,
            summaryPromptProvider,
            summarizer,
            artifactsProvider,
            hooks,
            cleanupHandlers);
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
        await _pipeline.CompactAsync(messages, OperationalContextCompactionReason.AutomaticThreshold, cancellationToken).ConfigureAwait(false);

    public async Task<OperationalContextCompactionResult> CompactReactiveAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default) =>
        await _pipeline.CompactAsync(messages, OperationalContextCompactionReason.Reactive, cancellationToken).ConfigureAwait(false);

    public static IReadOnlyList<ChatMessage> BuildMessages(OperationalContextCompactionResult result) =>
        CompactionMessageBuilder.Build(result);
}
