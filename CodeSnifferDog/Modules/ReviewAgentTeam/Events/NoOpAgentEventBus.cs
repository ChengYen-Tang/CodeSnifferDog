using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Modules.ReviewAgentTeam.Events;

/// <summary>
/// No-op event bus used when agent transcript and status events are intentionally disabled.
/// </summary>
internal sealed class NoOpAgentEventBus : IAgentEventBus
{
    /// <summary>
    /// Gets the shared singleton no-op event bus.
    /// </summary>
    public static NoOpAgentEventBus Instance { get; } = new();

    private static readonly IAgentEventScope NoOpScope = new NoOpAgentEventScope();

    /// <summary>
    /// Prevents external construction; use <see cref="Instance" />.
    /// </summary>
    private NoOpAgentEventBus()
    {
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="groupKey" /> or <paramref name="agentKey" /> is <see langword="null" />, empty, or whitespace.</exception>
    public IAgentEventScope CreateScope(string groupKey, string agentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentKey);
        return NoOpScope;
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
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Shared no-op scope that validates input and suppresses all event publication.
    /// </summary>
    private sealed class NoOpAgentEventScope : IAgentEventScope
    {
        /// <inheritdoc />
        public string GroupKey => string.Empty;

        /// <inheritdoc />
        public string AgentKey => string.Empty;

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
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="status" /> is <see langword="null" />, empty, or whitespace.</exception>
        public ValueTask PublishStatusChangedAsync(
            string status,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(status);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="message" /> is <see langword="null" />, empty, or whitespace.</exception>
        public ValueTask PublishUserMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="message" /> is <see langword="null" />, empty, or whitespace.</exception>
        public ValueTask PublishAssistantMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            return ValueTask.CompletedTask;
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
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="toolCallId" /> is <see langword="null" />, empty, or whitespace.</exception>
        public ValueTask PublishToolCallCompletedAsync(
            string toolCallId,
            string? result,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(toolCallId);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask PublishTranscriptClearedAsync(
            DateTimeOffset clearAfterUtc,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
