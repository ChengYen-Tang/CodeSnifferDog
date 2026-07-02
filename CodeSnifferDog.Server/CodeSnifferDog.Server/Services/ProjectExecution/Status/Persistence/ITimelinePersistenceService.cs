using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

internal interface ITimelinePersistenceService
{
    Task<TimelineEntryMutationResult> AppendTimelineEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ProjectAgentTimelineEntryType entryType,
        string? message,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);

    Task<TimelineEntryMutationResult> AppendToolCallStartedEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ToolCallStartedEvent agentEvent,
        CancellationToken cancellationToken);

    Task<TimelineEntryMutationResult> CompleteToolCallEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ToolCallCompletedEvent agentEvent,
        CancellationToken cancellationToken);

    Task<TimelineRemovalMutationResult?> RemoveTranscriptEntriesAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        TranscriptClearedEvent agentEvent,
        CancellationToken cancellationToken);
}
