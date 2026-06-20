using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

internal interface IAgentTimelinePersistenceService
{
    Task<AgentTimelineEntryMutationResult> AppendTimelineEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ProjectAgentTimelineEntryType entryType,
        string? message,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);

    Task<AgentTimelineEntryMutationResult> AppendToolCallStartedEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ToolCallStartedEvent agentEvent,
        CancellationToken cancellationToken);

    Task<AgentTimelineEntryMutationResult> CompleteToolCallEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ToolCallCompletedEvent agentEvent,
        CancellationToken cancellationToken);

    Task<AgentTimelineRemovalMutationResult?> RemoveTranscriptEntriesAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        AgentTranscriptClearedEvent agentEvent,
        CancellationToken cancellationToken);
}
