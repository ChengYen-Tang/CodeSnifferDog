namespace CodeSnifferDog.Models.ReviewAgentTeam;

/// <summary>
/// Publishes lifecycle and transcript events for one agent inside one agent group.
/// </summary>
public interface IAgentEventScope
{
    /// <summary>
    /// Gets the stable key of the owning agent group.
    /// </summary>
    string GroupKey { get; }

    /// <summary>
    /// Gets the stable key of the bound agent.
    /// </summary>
    string AgentKey { get; }

    /// <summary>
    /// Publishes the initial agent creation payload.
    /// </summary>
    /// <param name="displayName">Display name shown for the created agent.</param>
    /// <param name="systemPrompt">System prompt assigned to the created agent.</param>
    /// <param name="initialStatus">Initial status assigned to the created agent.</param>
    /// <param name="cancellationToken">Cancels event publication.</param>
    ValueTask PublishCreatedAsync(
        string displayName,
        string systemPrompt,
        string initialStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a status transition for the bound agent.
    /// </summary>
    /// <param name="status">New status value.</param>
    /// <param name="cancellationToken">Cancels event publication.</param>
    ValueTask PublishStatusChangedAsync(
        string status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a user message appended to the agent transcript.
    /// </summary>
    /// <param name="message">User message text.</param>
    /// <param name="cancellationToken">Cancels event publication.</param>
    ValueTask PublishUserMessageAsync(
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an assistant message appended to the agent transcript.
    /// </summary>
    /// <param name="message">Assistant message text.</param>
    /// <param name="cancellationToken">Cancels event publication.</param>
    ValueTask PublishAssistantMessageAsync(
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the start of a tool call emitted by the bound agent.
    /// </summary>
    /// <param name="toolCallId">Stable identifier of the tool call.</param>
    /// <param name="toolName">Name of the invoked tool.</param>
    /// <param name="arguments">Serialized tool arguments, when available.</param>
    /// <param name="cancellationToken">Cancels event publication.</param>
    ValueTask PublishToolCallStartedAsync(
        string toolCallId,
        string toolName,
        string? arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the completion of a tool call emitted by the bound agent.
    /// </summary>
    /// <param name="toolCallId">Stable identifier of the completed tool call.</param>
    /// <param name="result">Serialized tool result, when available.</param>
    /// <param name="cancellationToken">Cancels event publication.</param>
    ValueTask PublishToolCallCompletedAsync(
        string toolCallId,
        string? result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes that the bound agent compacted its transcript context.
    /// </summary>
    /// <param name="cancellationToken">Cancels event publication.</param>
    ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes that transcript messages older than the supplied cutoff were cleared.
    /// </summary>
    /// <param name="clearAfterUtc">Newest timestamp guaranteed to have been cleared from the transcript.</param>
    /// <param name="cancellationToken">Cancels event publication.</param>
    ValueTask PublishTranscriptClearedAsync(
        DateTimeOffset clearAfterUtc,
        CancellationToken cancellationToken = default);
}
