using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Compaction;

/// <summary>
/// Selects a preserved transcript tail using Agent Framework's atomic message groups.
/// </summary>
/// <remarks>
/// Agent Framework owns grouping and exclusion mechanics here. CodeSnifferDog still owns the
/// preserved-tail token/message policy and all summary, artifact, retry, rollback, and timeline
/// behavior around the selected messages.
/// </remarks>
internal sealed class FrameworkCompactionTailSelector(
    ILoggerFactory? loggerFactory = null) : ICompactionTailSelector
{
    private readonly ILogger? _logger = loggerFactory?.CreateLogger<FrameworkCompactionTailSelector>();

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ChatMessage>> SelectAsync(
        IReadOnlyList<ChatMessage> nonSystemMessages,
        CompactionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nonSystemMessages);
        ArgumentNullException.ThrowIfNull(options);

        if (nonSystemMessages.Count == 0)
            return [];

        FrameworkTailSelectionStrategy strategy = new(options);
        IEnumerable<ChatMessage> selectedMessages = await CompactionProvider
            .CompactAsync(
                strategy,
                nonSystemMessages,
                logger: _logger,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return [.. selectedMessages];
    }

    /// <summary>
    /// Applies the CodeSnifferDog tail policy to Framework-created message groups.
    /// </summary>
    private sealed class FrameworkTailSelectionStrategy(CompactionOptions options) : CompactionStrategy(CompactionTriggers.Always)
    {
        private readonly CompactionOptions _options = options;

        protected override ValueTask<bool> CompactCoreAsync(
            CompactionMessageIndex index,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<int> includedNonSystemGroupIndices = [];
            for (int indexPosition = 0; indexPosition < index.Groups.Count; indexPosition++)
            {
                CompactionMessageGroup group = index.Groups[indexPosition];
                if (!group.IsExcluded && group.Kind != CompactionGroupKind.System)
                    includedNonSystemGroupIndices.Add(indexPosition);
            }

            int firstSelectedGroupPosition = includedNonSystemGroupIndices.Count - 1;
            int totalTokens = 0;
            int messageCount = 0;

            for (int groupPosition = includedNonSystemGroupIndices.Count - 1; groupPosition >= 0; groupPosition--)
            {
                if (messageCount > 0 && totalTokens >= _options.PreservedTailMaxTokens)
                    break;

                CompactionMessageGroup group = index.Groups[includedNonSystemGroupIndices[groupPosition]];
                firstSelectedGroupPosition = groupPosition;
                totalTokens += EstimateGroupTokens(group);
                messageCount += group.MessageCount;

                bool reachedMinimumTail =
                    totalTokens >= _options.PreservedTailMinTokens &&
                    messageCount >= _options.PreservedTailMinMessages;

                if (reachedMinimumTail)
                    break;
            }

            bool excludedAnyGroup = false;
            for (int groupPosition = 0; groupPosition < firstSelectedGroupPosition; groupPosition++)
            {
                CompactionMessageGroup group = index.Groups[includedNonSystemGroupIndices[groupPosition]];
                group.IsExcluded = true;
                group.ExcludeReason = "Excluded by CodeSnifferDog preserved-tail policy";
                excludedAnyGroup = true;
            }

            return ValueTask.FromResult(excludedAnyGroup);
        }

        private static int EstimateGroupTokens(CompactionMessageGroup group)
        {
            int totalTokens = 0;
            foreach (ChatMessage message in group.Messages)
                totalTokens += TokenEstimator.Estimate([message]);

            return totalTokens;
        }
    }
}
