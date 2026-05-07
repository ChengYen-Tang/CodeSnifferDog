using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

public sealed class AgentCompactionEventHook(
    IAgentEventScope eventScope) : IOperationalContextCompactionHook
{
    private readonly IAgentEventScope _eventScope = eventScope;

    public ValueTask OnBeforeCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask OnAfterCompactionAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        IReadOnlyList<ChatMessage> compactedMessages,
        OperationalContextCompactionReason reason,
        CancellationToken cancellationToken) =>
        _eventScope.PublishCompactionAsync(cancellationToken);
}
