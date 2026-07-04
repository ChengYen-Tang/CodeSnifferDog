using CodeSnifferDog.Models.ReviewAgentTeam;
using System.Reactive.Subjects;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Modules.ReviewAgentTeam.Events;

/// <summary>
/// Publishes agent status events into a synchronized observable stream.
/// </summary>
public sealed class AgentStatusEventStream : IAgentEventBus, IDisposable
{
    private readonly Subject<StatusEvent> _innerSubject = new();
    private readonly ISubject<StatusEvent> _subject;
    private bool _disposed;

    /// <summary>
    /// Creates a synchronized status-event stream suitable for multi-threaded publishers.
    /// </summary>
    public AgentStatusEventStream()
    {
        _subject = Subject.Synchronize(_innerSubject);
    }

    /// <summary>
    /// Gets the observable event stream used by subscribers.
    /// </summary>
    internal IObservable<StatusEvent> Events => _subject;

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="groupKey" /> or <paramref name="agentKey" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">The stream has already been completed or disposed.</exception>
    public IAgentEventScope CreateScope(string groupKey, string agentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentKey);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new AgentEventScope(this, groupKey.Trim(), agentKey.Trim());
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="groupKey" /> or <paramref name="displayName" /> is <see langword="null" />, empty, or whitespace.</exception>
    public ValueTask PublishGroupCreatedAsync(
        string groupKey,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return PublishAsync(new GroupCreatedEvent
        {
            GroupKey = groupKey.Trim(),
            DisplayName = displayName.Trim(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken);
    }

    /// <summary>
    /// Publishes one concrete status event into the synchronized subject.
    /// </summary>
    /// <param name="agentEvent">Event to publish.</param>
    /// <param name="cancellationToken">Cancels event publication before the event is pushed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="agentEvent" /> is <see langword="null" />.</exception>
    /// <exception cref="ObjectDisposedException">The stream has already been completed or disposed.</exception>
    private ValueTask PublishAsync(StatusEvent agentEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentEvent);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        _subject.OnNext(agentEvent);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Completes the observable stream and prevents future publications.
    /// </summary>
    public void Complete()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subject.OnCompleted();
    }

    /// <summary>
    /// Disposes the underlying subject and prevents future publications.
    /// </summary>
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

        /// <inheritdoc />
        public string GroupKey { get; } = groupKey;

        /// <inheritdoc />
        public string AgentKey { get; } = agentKey;

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="displayName" />, <paramref name="systemPrompt" />, or <paramref name="initialStatus" /> is <see langword="null" />, empty, or whitespace.</exception>
        public ValueTask PublishCreatedAsync(
            string displayName,
            string systemPrompt,
            string initialStatus,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
            ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
            ArgumentException.ThrowIfNullOrWhiteSpace(initialStatus);

            return _bus.PublishAsync(new CreatedEvent
            {
                AgentKey = AgentKey,
                GroupKey = GroupKey,
                DisplayName = displayName.Trim(),
                SystemPrompt = systemPrompt,
                InitialStatus = initialStatus.Trim(),
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="status" /> is <see langword="null" />, empty, or whitespace.</exception>
        public ValueTask PublishStatusChangedAsync(
            string status,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(status);

            return _bus.PublishAsync(new StatusChangedEvent
            {
                GroupKey = GroupKey,
                AgentKey = AgentKey,
                Status = status.Trim(),
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="message" /> is <see langword="null" />, empty, or whitespace.</exception>
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

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="message" /> is <see langword="null" />, empty, or whitespace.</exception>
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

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="toolCallId" /> or <paramref name="toolName" /> is <see langword="null" />, empty, or whitespace.</exception>
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

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="toolCallId" /> is <see langword="null" />, empty, or whitespace.</exception>
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

        /// <inheritdoc />
        public ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default) =>
            _bus.PublishAsync(new CompactionEvent
            {
                GroupKey = GroupKey,
                AgentKey = AgentKey,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);

        /// <inheritdoc />
        public ValueTask PublishTranscriptClearedAsync(
            DateTimeOffset clearAfterUtc,
            CancellationToken cancellationToken = default) =>
            _bus.PublishAsync(new TranscriptClearedEvent
            {
                GroupKey = GroupKey,
                AgentKey = AgentKey,
                ClearAfterUtc = clearAfterUtc,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
    }
}
