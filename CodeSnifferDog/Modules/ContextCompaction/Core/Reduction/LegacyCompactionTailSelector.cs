using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Core.Transcript;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

/// <summary>
/// Preserves the original message-by-message tail policy for direct <see cref="ChatReducer"/> callers.
/// </summary>
internal sealed class LegacyCompactionTailSelector : ICompactionTailSelector
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ChatMessage>> SelectAsync(
        IReadOnlyList<ChatMessage> nonSystemMessages,
        CompactionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nonSystemMessages);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (nonSystemMessages.Count == 0)
            return ValueTask.FromResult<IReadOnlyList<ChatMessage>>([]);

        int firstSelectedMessageIndex = nonSystemMessages.Count;
        int totalTokens = 0;
        int messageCount = 0;

        for (int index = nonSystemMessages.Count - 1; index >= 0; index--)
        {
            ChatMessage message = nonSystemMessages[index];

            if (messageCount > 0 && totalTokens >= options.PreservedTailMaxTokens)
                break;

            firstSelectedMessageIndex = index;
            totalTokens += TokenEstimator.Estimate([message]);
            messageCount++;

            bool reachedMinimumTail =
                totalTokens >= options.PreservedTailMinTokens &&
                messageCount >= options.PreservedTailMinMessages;

            if (reachedMinimumTail)
                break;
        }

        int safeStartIndex = ToolCallTranscript.GetSafeTailStartIndex(
            nonSystemMessages,
            firstSelectedMessageIndex);

        return ValueTask.FromResult<IReadOnlyList<ChatMessage>>(
            [.. nonSystemMessages.Skip(safeStartIndex)]);
    }
}
