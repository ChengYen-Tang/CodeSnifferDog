using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

internal sealed class TimelinePersistenceService : ITimelinePersistenceService
{
    public async Task<TimelineEntryMutationResult> AppendTimelineEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ProjectAgentTimelineEntryType entryType,
        string? message,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ProjectAgentTimelineEntryRecord entry = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentId = agentId,
            Sequence = await GetNextSequenceAsync(dbContext, agentId, cancellationToken).ConfigureAwait(false),
            EntryType = entryType,
            Message = message,
            OccurredAtUtc = occurredAtUtc,
        };

        dbContext.ProjectAgentTimelineEntries.Add(entry);
        return new TimelineEntryMutationResult(entry);
    }

    public async Task<TimelineEntryMutationResult> AppendToolCallStartedEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ToolCallStartedEvent agentEvent,
        CancellationToken cancellationToken)
    {
        ProjectAgentTimelineEntryRecord entry = await GetOrCreateToolTimelineEntryAsync(
            dbContext,
            agentId,
            agentEvent.ToolCallId,
            agentEvent.OccurredAtUtc,
            cancellationToken).ConfigureAwait(false);

        entry.ToolName = agentEvent.ToolName;
        entry.ToolArguments = agentEvent.Arguments;
        return new TimelineEntryMutationResult(entry);
    }

    public async Task<TimelineEntryMutationResult> CompleteToolCallEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        ToolCallCompletedEvent agentEvent,
        CancellationToken cancellationToken)
    {
        ProjectAgentTimelineEntryRecord entry = await GetOrCreateToolTimelineEntryAsync(
            dbContext,
            agentId,
            agentEvent.ToolCallId,
            agentEvent.OccurredAtUtc,
            cancellationToken).ConfigureAwait(false);

        entry.ToolResult = agentEvent.Result;
        return new TimelineEntryMutationResult(entry);
    }

    public async Task<TimelineRemovalMutationResult?> RemoveTranscriptEntriesAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        TranscriptClearedEvent agentEvent,
        CancellationToken cancellationToken)
    {
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry =>
                entry.ProjectAgentId == agentId &&
                entry.EntryType != ProjectAgentTimelineEntryType.Input &&
                entry.OccurredAtUtc >= agentEvent.ClearAfterUtc)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entries.Count == 0)
            return null;

        Guid[] removedEntryIds = [.. entries.Select(static entry => entry.Id)];
        dbContext.ProjectAgentTimelineEntries.RemoveRange(entries);
        return new TimelineRemovalMutationResult(agentId, removedEntryIds, agentEvent.OccurredAtUtc);
    }

    private static async Task<ProjectAgentTimelineEntryRecord> GetOrCreateToolTimelineEntryAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        string toolCallId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ProjectAgentTimelineEntryRecord? existingEntry = await dbContext.ProjectAgentTimelineEntries
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.ProjectAgentId == agentId &&
                    candidate.EntryType == ProjectAgentTimelineEntryType.Tool &&
                    candidate.ToolCallId == toolCallId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existingEntry is not null)
            return existingEntry;

        ProjectAgentTimelineEntryRecord entry = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentId = agentId,
            Sequence = await GetNextSequenceAsync(dbContext, agentId, cancellationToken).ConfigureAwait(false),
            EntryType = ProjectAgentTimelineEntryType.Tool,
            ToolCallId = toolCallId,
            OccurredAtUtc = occurredAtUtc,
        };

        dbContext.ProjectAgentTimelineEntries.Add(entry);
        return entry;
    }

    private static async Task<long> GetNextSequenceAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        CancellationToken cancellationToken) =>
        (await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agentId)
            .MaxAsync(entry => (long?)entry.Sequence, cancellationToken)
            .ConfigureAwait(false) ?? 0) + 1;
}
