using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

/// <summary>
/// Persists timeline mutations for project agent events.
/// </summary>
internal interface ITimelinePersistenceService
{
    /// <summary>
    /// Appends a timeline entry for an agent.
    /// </summary>
    /// <param name="dbContext">Database context used for persistence.</param>
    /// <param name="agentId">Agent identifier that owns the timeline entry.</param>
    /// <param name="entryType">Timeline entry type to persist.</param>
    /// <param name="message">Optional timeline message payload.</param>
    /// <param name="occurredAtUtc">Timestamp that should be stored for the entry.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    /// <returns>The in-memory mutation result for the appended entry.</returns>
    Task<TimelineEntryMutationResult> AppendTimelineEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ProjectAgentTimelineEntryType entryType,
        string? message,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Appends or updates the timeline entry that represents a started tool call.
    /// </summary>
    /// <param name="dbContext">Database context used for persistence.</param>
    /// <param name="agentId">Agent identifier that owns the timeline entry.</param>
    /// <param name="agentEvent">Tool-call-started event to persist.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    /// <returns>The in-memory mutation result for the updated entry.</returns>
    Task<TimelineEntryMutationResult> AppendToolCallStartedEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ToolCallStartedEvent agentEvent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Completes the timeline entry that represents a tool call.
    /// </summary>
    /// <param name="dbContext">Database context used for persistence.</param>
    /// <param name="agentId">Agent identifier that owns the timeline entry.</param>
    /// <param name="agentEvent">Tool-call-completed event to persist.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    /// <returns>The in-memory mutation result for the updated entry.</returns>
    Task<TimelineEntryMutationResult> CompleteToolCallEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ToolCallCompletedEvent agentEvent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes persisted transcript entries after the specified clear point.
    /// </summary>
    /// <param name="dbContext">Database context used for persistence.</param>
    /// <param name="agentId">Agent identifier that owns the timeline entries.</param>
    /// <param name="agentEvent">Transcript-cleared event that defines the removal range.</param>
    /// <param name="cancellationToken">Token that cancels the persistence operation.</param>
    /// <returns>
    /// The in-memory mutation result for the removal, or <see langword="null"/> when no entries matched.
    /// </returns>
    Task<TimelineRemovalMutationResult?> RemoveTranscriptEntriesAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        TranscriptClearedEvent agentEvent,
        CancellationToken cancellationToken);
}
