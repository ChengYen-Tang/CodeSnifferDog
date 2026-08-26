using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Core.Transcript;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

/// <summary>
/// Preserves the original threshold and message-by-message tail policy for direct <see cref="ChatReducer"/> callers.
/// </summary>
internal sealed class LegacyCompactionPlanner(CompactionOptions options) : ICompactionPlanner
{
    private readonly CompactionOptions _options = options;

    /// <inheritdoc />
    public ValueTask<CompactionPlan> PlanAsync(
        IReadOnlyList<ChatMessage> messages,
        CompactionReason reason,
        int additionalEstimatedInputTokens,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();

        if (!ShouldCompact(messages, reason, additionalEstimatedInputTokens))
            return ValueTask.FromResult(CompactionPlan.Skip);

        List<ChatMessage> nonSystemMessages = new(messages.Count);
        foreach (ChatMessage message in messages)
        {
            if (message.Role != ChatRole.System)
                nonSystemMessages.Add(message);
        }

        IReadOnlyList<ChatMessage> messagesToKeep = SelectTail(nonSystemMessages, cancellationToken);
        return ValueTask.FromResult(new CompactionPlan(true, messagesToKeep));
    }

    /// <summary>
    /// Applies the previous message-by-message preserved-tail policy, expanding the result to avoid splitting tool calls.
    /// </summary>
    private IReadOnlyList<ChatMessage> SelectTail(
        IReadOnlyList<ChatMessage> nonSystemMessages,
        CancellationToken cancellationToken)
    {
        if (nonSystemMessages.Count == 0)
            return [];

        int firstSelectedMessageIndex = nonSystemMessages.Count;
        int totalTokens = 0;
        int messageCount = 0;

        for (int index = nonSystemMessages.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ChatMessage message = nonSystemMessages[index];

            if (messageCount > 0 && totalTokens >= _options.PreservedTailMaxTokens)
                break;

            firstSelectedMessageIndex = index;
            totalTokens += TokenEstimator.Estimate([message]);
            messageCount++;

            bool reachedMinimumTail =
                totalTokens >= _options.PreservedTailMinTokens &&
                messageCount >= _options.PreservedTailMinMessages;

            if (reachedMinimumTail)
                break;
        }

        int safeStartIndex = ToolCallTranscript.GetSafeTailStartIndex(
            nonSystemMessages,
            firstSelectedMessageIndex);

        return [.. nonSystemMessages.Skip(safeStartIndex)];
    }

    /// <summary>
    /// Retains the existing automatic threshold and provider input-token bias calculation.
    /// </summary>
    private bool ShouldCompact(
        IEnumerable<ChatMessage> messages,
        CompactionReason reason,
        int additionalEstimatedInputTokens)
    {
        if (reason == CompactionReason.Reactive)
            return true;

        long estimatedTokens = TokenEstimator.Estimate(messages);
        long adjustedEstimate = estimatedTokens + Math.Max(0, additionalEstimatedInputTokens);
        return adjustedEstimate >= _options.GetAutoCompactThreshold();
    }
}
