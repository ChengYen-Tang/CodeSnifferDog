using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

internal sealed class NoOpAgentEventBus : IAgentEventBus
{
    public static NoOpAgentEventBus Instance { get; } = new();

    private static readonly IAgentEventScope NoOpScope = new NoOpAgentEventScope();

    private NoOpAgentEventBus()
    {
    }

    public IAgentEventScope CreateScope(string groupKey, string agentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentKey);
        return NoOpScope;
    }

    public ValueTask PublishGroupCreatedAsync(
        string groupKey,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return ValueTask.CompletedTask;
    }

    private sealed class NoOpAgentEventScope : IAgentEventScope
    {
        public string GroupKey => string.Empty;

        public string AgentKey => string.Empty;

        public ValueTask PublishCreatedAsync(
            string displayName,
            string initialStatus,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
            ArgumentException.ThrowIfNullOrWhiteSpace(initialStatus);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishStatusChangedAsync(
            string status,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(status);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishUserMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAssistantMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            return ValueTask.CompletedTask;
        }

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

        public ValueTask PublishToolCallCompletedAsync(
            string toolCallId,
            string? result,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(toolCallId);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
