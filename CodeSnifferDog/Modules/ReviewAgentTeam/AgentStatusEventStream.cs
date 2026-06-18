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
            string systemPrompt,
            string initialStatus,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
            ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
            ArgumentException.ThrowIfNullOrWhiteSpace(initialStatus);

            return _bus.PublishAsync(new AgentCreatedEvent
            {
                AgentKey = AgentKey,
                GroupKey = GroupKey,
                DisplayName = displayName.Trim(),
                SystemPrompt = systemPrompt,
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

        public ValueTask PublishToolCallStartedAsync(
            string toolCallId,
            string toolName,
            string? arguments,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(toolCallId);
            ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

            return _bus.PublishAsync(new ToolCallStartedEvent
            {
                GroupKey = GroupKey,
                AgentKey = AgentKey,
                ToolCallId = toolCallId.Trim(),
                ToolName = toolName.Trim(),
                Arguments = arguments,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }

        public ValueTask PublishToolCallCompletedAsync(
            string toolCallId,
            string? result,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(toolCallId);

            return _bus.PublishAsync(new ToolCallCompletedEvent
            {
                GroupKey = GroupKey,
                AgentKey = AgentKey,
                ToolCallId = toolCallId.Trim(),
                Result = result,
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

        public ValueTask PublishTranscriptClearedAsync(
            DateTimeOffset clearAfterUtc,
            CancellationToken cancellationToken = default) =>
            _bus.PublishAsync(new AgentTranscriptClearedEvent
            {
                GroupKey = GroupKey,
                AgentKey = AgentKey,
                ClearAfterUtc = clearAfterUtc,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
    }
}
