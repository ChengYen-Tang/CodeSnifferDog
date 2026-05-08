using CodeSnifferDog.Models.ReviewAgentTeam;
using System.Reactive.Subjects;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

public sealed class AgentStatusEventStream : IAgentEventBus, IDisposable
{
    private readonly Subject<AgentStatusEvent> _innerSubject = new();
    private readonly ISubject<AgentStatusEvent> _subject;
    private bool _disposed;

    public AgentStatusEventStream()
    {
        _subject = Subject.Synchronize(_innerSubject);
    }

    internal IObservable<AgentStatusEvent> Events => _subject;

    public IAgentEventScope CreateScope(string groupKey, string agentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentKey);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new AgentEventScope(this, groupKey.Trim(), agentKey.Trim());
    }

    public ValueTask PublishGroupCreatedAsync(
        string groupKey,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return PublishAsync(new AgentGroupCreatedEvent
        {
            GroupKey = groupKey.Trim(),
            DisplayName = displayName.Trim(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken);
    }

    private ValueTask PublishAsync(AgentStatusEvent agentEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentEvent);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        _subject.OnNext(agentEvent);
        return ValueTask.CompletedTask;
    }

    public void Complete()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subject.OnCompleted();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _innerSubject.Dispose();
    }

    private sealed class AgentEventScope(AgentStatusEventStream bus, string groupKey, string agentKey) : IAgentEventScope
    {
        private readonly AgentStatusEventStream _bus = bus;

        public string GroupKey { get; } = groupKey;

        public string AgentKey { get; } = agentKey;

        public ValueTask PublishCreatedAsync(
            string displayName,
            string initialStatus,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
            ArgumentException.ThrowIfNullOrWhiteSpace(initialStatus);

            return _bus.PublishAsync(new AgentCreatedEvent
            {
                AgentKey = AgentKey,
                GroupKey = GroupKey,
                DisplayName = displayName.Trim(),
                InitialStatus = initialStatus.Trim(),
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }

        public ValueTask PublishStatusChangedAsync(
            string status,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(status);

            return _bus.PublishAsync(new AgentStatusChangedEvent
            {
                GroupKey = GroupKey,
                AgentKey = AgentKey,
                Status = status.Trim(),
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }

        public ValueTask PublishUserMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            return _bus.PublishAsync(new UserMessageAppendedEvent
            {
                GroupKey = GroupKey,
                AgentKey = AgentKey,
                Message = message.Trim(),
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }

        public ValueTask PublishAssistantMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            return _bus.PublishAsync(new AssistantMessageAppendedEvent
            {
                GroupKey = GroupKey,
                AgentKey = AgentKey,
                Message = message.Trim(),
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }

        public ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default) =>
            _bus.PublishAsync(new AgentCompactionEvent
            {
                GroupKey = GroupKey,
                AgentKey = AgentKey,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
    }
}
