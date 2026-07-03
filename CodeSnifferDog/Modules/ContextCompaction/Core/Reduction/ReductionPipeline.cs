using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

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

        await _hookDispatcher.RunBeforeCompactionAsync(materializedMessages, reason, cancellationToken).ConfigureAwait(false);

        string summaryPrompt = SummaryContract.BuildPrompt(
            await summaryPromptProvider.GetPromptAsync(cancellationToken).ConfigureAwait(false));

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
