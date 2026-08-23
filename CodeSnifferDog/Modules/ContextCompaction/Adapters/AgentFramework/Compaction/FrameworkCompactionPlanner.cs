using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Compaction;

/// <summary>
/// Plans compaction with Agent Framework's atomic message groups while preserving CodeSnifferDog's trigger semantics.
/// </summary>
/// <remarks>
/// Agent Framework owns tool-call grouping and exclusion mechanics. CodeSnifferDog retains its JSON-aware token
/// estimator, provider input-token bias, preserved-tail policy, summary contract, artifacts, retry, rollback, and
/// timeline behavior.
/// </remarks>
internal sealed class FrameworkCompactionPlanner(
    CompactionOptions options,
    ILoggerFactory? loggerFactory = null) : ICompactionPlanner
{
    private readonly CompactionOptions _options = options;
    private readonly ILogger? _logger = loggerFactory?.CreateLogger<FrameworkCompactionPlanner>();
    private readonly LegacyCompactionPlanner _legacyPlanner = new(options);

    /// <inheritdoc />
    public async ValueTask<CompactionPlan> PlanAsync(
        IReadOnlyList<ChatMessage> messages,
        CompactionReason reason,
        int additionalEstimatedInputTokens,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();

        RecordingTrigger trigger = new(CreateTrigger(reason, additionalEstimatedInputTokens));
        FrameworkTailPlanningStrategy strategy = new(_options, trigger.Evaluate);

        _ = await CompactionProvider
            .CompactAsync(
                strategy,
                messages,
                logger: _logger,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // CompactionStrategy deliberately skips its trigger when Framework finds zero or one non-system group.
        // Fall back to the legacy planner so a single complete turn retains the existing compaction behavior.
        if (!trigger.WasEvaluated)
        {
            return await _legacyPlanner
                .PlanAsync(messages, reason, additionalEstimatedInputTokens, cancellationToken)
                .ConfigureAwait(false);
        }

        return trigger.WasTriggered
            ? new CompactionPlan(true, strategy.MessagesToKeep)
            : CompactionPlan.Skip;
    }

    /// <summary>
    /// Creates a Framework trigger that retains CodeSnifferDog's automatic threshold semantics.
    /// </summary>
    private CompactionTrigger CreateTrigger(
        CompactionReason reason,
        int additionalEstimatedInputTokens) =>
        reason == CompactionReason.Reactive
            ? CompactionTriggers.Always
            : index =>
            {
                long estimatedTokens = TokenEstimator.Estimate(index.GetIncludedMessages());
                long adjustedEstimate = estimatedTokens + Math.Max(0, additionalEstimatedInputTokens);
                return adjustedEstimate >= _options.GetAutoCompactThreshold();
            };

    /// <summary>
    /// Records the Framework trigger evaluation so the zero-or-one-group guard can fall back safely.
    /// </summary>
    private sealed class RecordingTrigger(CompactionTrigger inner)
    {
        private readonly CompactionTrigger _inner = inner;

        public bool WasEvaluated { get; private set; }

        public bool WasTriggered { get; private set; }

        public bool Evaluate(CompactionMessageIndex index)
        {
            WasEvaluated = true;
            WasTriggered = _inner(index);
            return WasTriggered;
        }
    }

    /// <summary>
    /// Applies the CodeSnifferDog tail policy to Framework-created message groups.
    /// </summary>
    private sealed class FrameworkTailPlanningStrategy(
        CompactionOptions options,
        CompactionTrigger trigger) : CompactionStrategy(trigger)
    {
        private readonly CompactionOptions _options = options;

        public IReadOnlyList<ChatMessage> MessagesToKeep { get; private set; } = [];

        protected override ValueTask<bool> CompactCoreAsync(
            CompactionMessageIndex index,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            List<int> includedNonSystemGroupIndices = [];
            for (int indexPosition = 0; indexPosition < index.Groups.Count; indexPosition++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                CompactionMessageGroup group = index.Groups[indexPosition];
                if (!group.IsExcluded && group.Kind != CompactionGroupKind.System)
                    includedNonSystemGroupIndices.Add(indexPosition);
            }

            int firstSelectedGroupPosition = includedNonSystemGroupIndices.Count - 1;
            int totalTokens = 0;
            int messageCount = 0;

            for (int groupPosition = includedNonSystemGroupIndices.Count - 1; groupPosition >= 0; groupPosition--)
            {
                cancellationToken.ThrowIfCancellationRequested();

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

            List<ChatMessage> messagesToKeep = new(messageCount);
            for (int groupPosition = firstSelectedGroupPosition;
                 groupPosition < includedNonSystemGroupIndices.Count;
                 groupPosition++)
            {
                CompactionMessageGroup group = index.Groups[includedNonSystemGroupIndices[groupPosition]];
                messagesToKeep.AddRange(group.Messages);
            }

            MessagesToKeep = messagesToKeep;

            return ValueTask.FromResult(excludedAnyGroup);
        }

        private static int EstimateGroupTokens(CompactionMessageGroup group) =>
            TokenEstimator.Estimate(group.Messages);
    }
}
