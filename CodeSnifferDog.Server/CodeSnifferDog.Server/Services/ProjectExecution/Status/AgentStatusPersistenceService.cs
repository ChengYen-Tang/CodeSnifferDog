using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using IProjectAgentStatusLiveUpdateNotifier = CodeSnifferDog.Server.Services.ProjectAgentStatus.IProjectAgentStatusLiveUpdateNotifier;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal sealed class AgentStatusPersistenceService(
    Guid projectId,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier,
    AgentStatusLiveUpdateFactory liveUpdateFactory) : IAgentStatusPersistenceService
{
    private readonly Guid _projectId = projectId;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectAgentStatusLiveUpdateNotifier _liveUpdateNotifier = liveUpdateNotifier;
    private readonly AgentStatusLiveUpdateFactory _liveUpdateFactory = liveUpdateFactory;

    public async Task UpsertGroupAsync(AgentGroupCreatedEvent agentEvent, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentGroupRecord? existingGroup = await dbContext.ProjectAgentGroups
            .SingleOrDefaultAsync(
                group => group.ProjectId == _projectId && group.RuntimeKey == agentEvent.GroupKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (existingGroup is not null)
        {
            existingGroup.DisplayName = agentEvent.DisplayName;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await NotifyAsync(_liveUpdateFactory.CreateGroupUpdate(_projectId, existingGroup), cancellationToken).ConfigureAwait(false);
            return;
        }

        ProjectAgentGroupRecord group = new()
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            RuntimeKey = agentEvent.GroupKey,
            DisplayName = agentEvent.DisplayName,
            CreatedAtUtc = agentEvent.OccurredAtUtc,
        };

        dbContext.ProjectAgentGroups.Add(group);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateGroupUpdate(_projectId, group), cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertAgentAsync(AgentCreatedEvent agentEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agentEvent.GroupKey))
            throw new InvalidOperationException("AgentCreatedEvent.GroupKey is required for persistence.");

        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(
                candidate => candidate.ProjectId == _projectId && candidate.RuntimeKey == agentEvent.GroupKey,
                cancellationToken)
            .ConfigureAwait(false);

        ProjectAgentRecord? existingAgent = await dbContext.ProjectAgents
            .SingleOrDefaultAsync(
                agent => agent.ProjectAgentGroupId == group.Id && agent.RuntimeKey == agentEvent.AgentKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (existingAgent is not null)
        {
            existingAgent.DisplayName = agentEvent.DisplayName;
            existingAgent.SystemPrompt = agentEvent.SystemPrompt;
            existingAgent.Status = ParseStatus(agentEvent.InitialStatus);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await NotifyAsync(_liveUpdateFactory.CreateAgentUpsertUpdate(_projectId, existingAgent), cancellationToken).ConfigureAwait(false);
            return;
        }

        ProjectAgentRecord agent = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentGroupId = group.Id,
            RuntimeKey = agentEvent.AgentKey,
            DisplayName = agentEvent.DisplayName,
            SystemPrompt = agentEvent.SystemPrompt,
            Status = ParseStatus(agentEvent.InitialStatus),
            CreatedAtUtc = agentEvent.OccurredAtUtc,
        };

        dbContext.ProjectAgents.Add(agent);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateAgentUpsertUpdate(_projectId, agent), cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAgentStatusAsync(AgentStatusChangedEvent agentEvent, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, agentEvent.GroupKey, agentEvent.AgentKey, cancellationToken).ConfigureAwait(false);

        agent.Status = ParseStatus(agentEvent.Status);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateAgentStatusChangedUpdate(_projectId, agent.Id, agent.Status, agentEvent.OccurredAtUtc), cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveTranscriptEntriesAsync(
        AgentTranscriptClearedEvent agentEvent,
        CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, agentEvent.GroupKey, agentEvent.AgentKey, cancellationToken).ConfigureAwait(false);
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry =>
                entry.ProjectAgentId == agent.Id &&
                entry.EntryType != ProjectAgentTimelineEntryType.Input &&
                entry.OccurredAtUtc >= agentEvent.ClearAfterUtc)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entries.Count == 0)
            return;

        Guid[] removedEntryIds = [.. entries.Select(static entry => entry.Id)];
        dbContext.ProjectAgentTimelineEntries.RemoveRange(entries);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateTimelineEntriesRemovedUpdate(_projectId, agent.Id, removedEntryIds, agentEvent.OccurredAtUtc), cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendToolCallStartedEntryAsync(ToolCallStartedEvent agentEvent, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, agentEvent.GroupKey, agentEvent.AgentKey, cancellationToken).ConfigureAwait(false);
        ProjectAgentTimelineEntryRecord entry = await GetOrCreateToolTimelineEntryAsync(
            dbContext,
            agent.Id,
            agentEvent.ToolCallId,
            agentEvent.OccurredAtUtc,
            cancellationToken).ConfigureAwait(false);

        entry.ToolName = agentEvent.ToolName;
        entry.ToolArguments = agentEvent.Arguments;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateTimelineEntryUpsertUpdate(_projectId, entry), cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteToolCallEntryAsync(ToolCallCompletedEvent agentEvent, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, agentEvent.GroupKey, agentEvent.AgentKey, cancellationToken).ConfigureAwait(false);
        ProjectAgentTimelineEntryRecord entry = await GetOrCreateToolTimelineEntryAsync(
            dbContext,
            agent.Id,
            agentEvent.ToolCallId,
            agentEvent.OccurredAtUtc,
            cancellationToken).ConfigureAwait(false);

        entry.ToolResult = agentEvent.Result;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateTimelineEntryUpsertUpdate(_projectId, entry), cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendTimelineEntryAsync(
        string groupKey,
        string agentKey,
        ProjectAgentTimelineEntryType entryType,
        string? message,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, groupKey, agentKey, cancellationToken).ConfigureAwait(false);
        long nextSequence = (await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agent.Id)
            .MaxAsync(entry => (long?)entry.Sequence, cancellationToken)
            .ConfigureAwait(false) ?? 0) + 1;

        ProjectAgentTimelineEntryRecord entry = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentId = agent.Id,
            Sequence = nextSequence,
            EntryType = entryType,
            Message = message,
            OccurredAtUtc = occurredAtUtc,
        };

        dbContext.ProjectAgentTimelineEntries.Add(entry);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateTimelineEntryUpsertUpdate(_projectId, entry), cancellationToken).ConfigureAwait(false);
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

        long nextSequence = (await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agentId)
            .MaxAsync(entry => (long?)entry.Sequence, cancellationToken)
            .ConfigureAwait(false) ?? 0) + 1;

        ProjectAgentTimelineEntryRecord entry = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentId = agentId,
            Sequence = nextSequence,
            EntryType = ProjectAgentTimelineEntryType.Tool,
            ToolCallId = toolCallId,
            OccurredAtUtc = occurredAtUtc,
        };

        dbContext.ProjectAgentTimelineEntries.Add(entry);
        return entry;
    }

    private async Task<ProjectAgentRecord> GetAgentAsync(
        CodeSnifferDogServerDbContext dbContext,
        string groupKey,
        string agentKey,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectAgents
            .Include(candidate => candidate.Group)
            .SingleAsync(
                candidate =>
                    candidate.Group != null &&
                    candidate.Group.ProjectId == _projectId &&
                    candidate.Group.RuntimeKey == groupKey &&
                    candidate.RuntimeKey == agentKey,
                cancellationToken)
            .ConfigureAwait(false);

    internal static Data.Entities.ProjectAgentStatus ParseStatus(string status) =>
        status.Trim() switch
        {
            "Waiting" => Data.Entities.ProjectAgentStatus.Waiting,
            "Running" => Data.Entities.ProjectAgentStatus.Running,
            "Completed" => Data.Entities.ProjectAgentStatus.Completed,
            "Degraded" => Data.Entities.ProjectAgentStatus.Degraded,
            _ => throw new InvalidOperationException($"Unsupported agent status '{status}'."),
        };

    private Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken) =>
        _liveUpdateNotifier.NotifyAsync(update, cancellationToken);
}
