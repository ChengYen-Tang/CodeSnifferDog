using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

public sealed class AgentCompactionEventHook(
    IAgentEventScope eventScope,
    ILogger<AgentCompactionEventHook>? logger = null) : IOperationalContextCompactionHook
{
    private readonly IAgentEventScope _eventScope = eventScope;
    private readonly ILogger<AgentCompactionEventHook>? _logger = logger;

    public ValueTask OnBeforeCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask OnAfterCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        OperationalContextCompactionReason reason,
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
