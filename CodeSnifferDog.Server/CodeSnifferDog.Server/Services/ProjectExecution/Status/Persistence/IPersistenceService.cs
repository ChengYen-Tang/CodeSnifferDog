using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

internal interface IPersistenceService
{
    Task UpsertGroupAsync(GroupCreatedEvent agentEvent, CancellationToken cancellationToken);

    Task UpsertAgentAsync(CreatedEvent agentEvent, CancellationToken cancellationToken);

    Task UpdateAgentStatusAsync(StatusChangedEvent agentEvent, CancellationToken cancellationToken);

    Task AppendToolCallStartedEntryAsync(ToolCallStartedEvent agentEvent, CancellationToken cancellationToken);

    Task CompleteToolCallEntryAsync(ToolCallCompletedEvent agentEvent, CancellationToken cancellationToken);

    Task RemoveTranscriptEntriesAsync(TranscriptClearedEvent agentEvent, CancellationToken cancellationToken);

    Task AppendTimelineEntryAsync(
        string groupKey,
        string agentKey,
        ProjectAgentTimelineEntryType entryType,
        string? message,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);
}
