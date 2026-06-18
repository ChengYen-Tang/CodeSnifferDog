namespace CodeSnifferDog.Models.ReviewAgentTeam;

public interface IAgentEventScope
{
    string GroupKey { get; }

    string AgentKey { get; }

    ValueTask PublishCreatedAsync(
        string displayName,
        string systemPrompt,
        string initialStatus,
        CancellationToken cancellationToken = default);

    ValueTask PublishStatusChangedAsync(
        string status,
        CancellationToken cancellationToken = default);

    ValueTask PublishUserMessageAsync(
        string message,
        CancellationToken cancellationToken = default);

    ValueTask PublishAssistantMessageAsync(
        string message,
        CancellationToken cancellationToken = default);

    ValueTask PublishToolCallStartedAsync(
        string toolCallId,
        string toolName,
        string? arguments,
        CancellationToken cancellationToken = default);

    ValueTask PublishToolCallCompletedAsync(
        string toolCallId,
        string? result,
        CancellationToken cancellationToken = default);

    ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default);

    ValueTask PublishTranscriptClearedAsync(
        DateTimeOffset clearAfterUtc,
        CancellationToken cancellationToken = default);
}
