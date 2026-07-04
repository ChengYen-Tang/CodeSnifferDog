using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

/// <summary>
/// Persists project agent status events and emits the corresponding live updates.
/// </summary>
internal interface IPersistenceService
{
    /// <summary>
    /// Creates or updates an agent group for the current project.
    /// </summary>
    /// <param name="agentEvent">Group-created event to persist.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    Task UpsertGroupAsync(GroupCreatedEvent agentEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or updates an agent for the current project.
    /// </summary>
    /// <param name="agentEvent">Agent-created event to persist.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="CreatedEvent.GroupKey"/> is missing and the agent cannot be associated with a group.
    /// </exception>
    Task UpsertAgentAsync(CreatedEvent agentEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the status of an existing agent.
    /// </summary>
    /// <param name="agentEvent">Status-changed event to persist.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    Task UpdateAgentStatusAsync(StatusChangedEvent agentEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Appends or updates the timeline entry that represents a started tool call.
    /// </summary>
    /// <param name="agentEvent">Tool-call-started event to persist.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    Task AppendToolCallStartedEntryAsync(ToolCallStartedEvent agentEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Completes the timeline entry that represents a tool call.
    /// </summary>
    /// <param name="agentEvent">Tool-call-completed event to persist.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    Task CompleteToolCallEntryAsync(ToolCallCompletedEvent agentEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Removes persisted transcript entries after the specified clear point.
    /// </summary>
    /// <param name="agentEvent">Transcript-cleared event that defines the removal range.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    Task RemoveTranscriptEntriesAsync(TranscriptClearedEvent agentEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Appends a non-tool timeline entry for an agent.
    /// </summary>
    /// <param name="groupKey">Runtime key of the agent group.</param>
    /// <param name="agentKey">Runtime key of the agent.</param>
    /// <param name="entryType">Timeline entry type to persist.</param>
    /// <param name="message">Optional timeline message payload.</param>
    /// <param name="occurredAtUtc">Timestamp that should be stored for the entry.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    Task AppendTimelineEntryAsync(
        string groupKey,
        string agentKey,
        ProjectAgentTimelineEntryType entryType,
        string? message,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);
}
