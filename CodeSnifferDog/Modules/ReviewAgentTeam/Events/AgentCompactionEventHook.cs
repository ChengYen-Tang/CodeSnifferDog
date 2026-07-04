using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ReviewAgentTeam.Events;

/// <summary>
/// Publishes an agent-event-bus compaction event after transcript compaction completes.
/// </summary>
/// <param name="eventScope">Event scope that should receive the compaction event.</param>
/// <param name="logger">Optional logger used to record compaction diagnostics.</param>
public sealed class AgentCompactionEventHook(
    IAgentEventScope eventScope,
    ILogger<AgentCompactionEventHook>? logger = null) : IHook
{
    private readonly IAgentEventScope _eventScope = eventScope;
    private readonly ILogger<AgentCompactionEventHook>? _logger = logger;

    /// <inheritdoc />
    public ValueTask OnBeforeCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        CompactionReason reason,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    /// <remarks>
    /// The hook emits only after-compaction events because downstream consumers care about successful compaction
    /// boundaries, not about every attempted compaction start.
    /// </remarks>
    public ValueTask OnAfterCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        CompactionReason reason,
        CancellationToken cancellationToken)
    {
        _logger?.LogDebug(
            "Agent context compaction occurred for group {GroupKey}, agent {AgentKey}. Reason: {CompactionReason}; original messages: {OriginalMessageCount}; compacted messages: {CompactedMessageCount}.",
            _eventScope.GroupKey,
            _eventScope.AgentKey,
            reason,
            originalMessages.Count,
            compactedMessages.Count);

        return _eventScope.PublishCompactionAsync(cancellationToken);
    }
}
