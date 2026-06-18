using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using IProjectAgentStatusLiveUpdateNotifier = CodeSnifferDog.Server.Services.ProjectAgentStatus.IProjectAgentStatusLiveUpdateNotifier;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal sealed class ProjectAgentStatusEventSubscriber : IAsyncDisposable
{
    private readonly Guid _projectId;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory;
    private readonly IProjectAgentStatusLiveUpdateNotifier _liveUpdateNotifier;
    private readonly IDisposable _subscription;
    private readonly object _sync = new();
    private Task _processingTail = Task.CompletedTask;
    private bool _disposed;

    public ProjectAgentStatusEventSubscriber(
        Guid projectId,
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        IProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier,
        IObservable<AgentStatusEvent> events)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(liveUpdateNotifier);
        ArgumentNullException.ThrowIfNull(events);

        _projectId = projectId;
        _dbContextFactory = dbContextFactory;
        _liveUpdateNotifier = liveUpdateNotifier;
        _subscription = events.Subscribe(Enqueue);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subscription.Dispose();
        Task tail;
        lock (_sync)
            tail = _processingTail;

        await tail.ConfigureAwait(false);
    }

    private void Enqueue(AgentStatusEvent agentEvent)
    {
        lock (_sync)
        {
            _processingTail = _processingTail
                .ContinueWith(
                    _ => HandleAsync(agentEvent, CancellationToken.None),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default)
                .Unwrap();
        }
    }

    private async Task HandleAsync(AgentStatusEvent agentEvent, CancellationToken cancellationToken)
    {
        switch (agentEvent)
        {
            case AgentGroupCreatedEvent groupCreatedEvent:
                await UpsertGroupAsync(groupCreatedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case AgentCreatedEvent agentCreatedEvent:
                await UpsertAgentAsync(agentCreatedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case AgentStatusChangedEvent statusChangedEvent:
                await UpdateAgentStatusAsync(statusChangedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case UserMessageAppendedEvent userMessageEvent:
                await AppendTimelineEntryAsync(
                    userMessageEvent.GroupKey,
                    userMessageEvent.AgentKey,
                    ProjectAgentTimelineEntryType.Input,
                    userMessageEvent.Message,
                    userMessageEvent.OccurredAtUtc,
                    cancellationToken).ConfigureAwait(false);
                return;

            case AssistantMessageAppendedEvent assistantMessageEvent:
                await AppendTimelineEntryAsync(
                    assistantMessageEvent.GroupKey,
                    assistantMessageEvent.AgentKey,
                    ProjectAgentTimelineEntryType.Output,
                    assistantMessageEvent.Message,
                    assistantMessageEvent.OccurredAtUtc,
                    cancellationToken).ConfigureAwait(false);
                return;

            case ToolCallStartedEvent toolCallStartedEvent:
                await AppendToolCallStartedEntryAsync(toolCallStartedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case ToolCallCompletedEvent toolCallCompletedEvent:
                await CompleteToolCallEntryAsync(toolCallCompletedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case AgentCompactionEvent compactionEvent:
                await AppendCompactionEntryAsync(compactionEvent, cancellationToken).ConfigureAwait(false);
                return;

            case AgentTranscriptClearedEvent transcriptClearedEvent:
                await RemoveTranscriptEntriesAsync(transcriptClearedEvent, cancellationToken).ConfigureAwait(false);
                return;
        }
    }

    private async Task UpsertGroupAsync(AgentGroupCreatedEvent agentEvent, CancellationToken cancellationToken)
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
            await NotifyAsync(CreateGroupUpdate(existingGroup), cancellationToken).ConfigureAwait(false);
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
        await NotifyAsync(CreateGroupUpdate(group), cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertAgentAsync(AgentCreatedEvent agentEvent, CancellationToken cancellationToken)
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
            await NotifyAsync(CreateAgentUpsertUpdate(existingAgent), cancellationToken).ConfigureAwait(false);
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
        await NotifyAsync(CreateAgentUpsertUpdate(agent), cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAgentStatusAsync(AgentStatusChangedEvent agentEvent, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, agentEvent.GroupKey, agentEvent.AgentKey, cancellationToken).ConfigureAwait(false);

        agent.Status = ParseStatus(agentEvent.Status);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(CreateAgentStatusChangedUpdate(agent.Id, agent.Status, agentEvent.OccurredAtUtc), cancellationToken).ConfigureAwait(false);
    }

    private async Task AppendCompactionEntryAsync(AgentCompactionEvent agentEvent, CancellationToken cancellationToken)
    {
        await AppendTimelineEntryAsync(
            agentEvent.GroupKey,
            agentEvent.AgentKey,
            ProjectAgentTimelineEntryType.Compaction,
            message: null,
            agentEvent.OccurredAtUtc,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveTranscriptEntriesAsync(
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
        await NotifyAsync(CreateTimelineEntriesRemovedUpdate(agent.Id, removedEntryIds, agentEvent.OccurredAtUtc), cancellationToken).ConfigureAwait(false);
    }

    private async Task AppendToolCallStartedEntryAsync(ToolCallStartedEvent agentEvent, CancellationToken cancellationToken)
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
        await NotifyAsync(CreateTimelineEntryUpsertUpdate(entry), cancellationToken).ConfigureAwait(false);
    }

    private async Task CompleteToolCallEntryAsync(ToolCallCompletedEvent agentEvent, CancellationToken cancellationToken)
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
        await NotifyAsync(CreateTimelineEntryUpsertUpdate(entry), cancellationToken).ConfigureAwait(false);
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

    private async Task AppendTimelineEntryAsync(
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
        await NotifyAsync(CreateTimelineEntryUpsertUpdate(entry), cancellationToken).ConfigureAwait(false);
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

    private static Data.Entities.ProjectAgentStatus ParseStatus(string status) =>
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

    private ProjectAgentLiveUpdateDto CreateGroupUpdate(ProjectAgentGroupRecord group) =>
        new()
        {
            ProjectId = _projectId,
            Kind = ProjectAgentLiveUpdateKind.AgentGroupUpserted,
            OccurredAtUtc = group.CreatedAtUtc,
            Group = new ProjectAgentGroupLiveDto
            {
                GroupId = group.Id,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
            },
        };

    private ProjectAgentLiveUpdateDto CreateAgentUpsertUpdate(ProjectAgentRecord agent) =>
        new()
        {
            ProjectId = _projectId,
            Kind = ProjectAgentLiveUpdateKind.AgentUpserted,
            OccurredAtUtc = agent.CreatedAtUtc,
            Agent = new ProjectAgentLiveDto
            {
                AgentId = agent.Id,
                GroupId = agent.ProjectAgentGroupId,
                RuntimeKey = agent.RuntimeKey,
                DisplayName = agent.DisplayName,
                SystemPrompt = agent.SystemPrompt,
                Status = MapAgentStatus(agent.Status),
                CreatedAtUtc = agent.CreatedAtUtc,
            },
        };

    private ProjectAgentLiveUpdateDto CreateAgentStatusChangedUpdate(Guid agentId, Data.Entities.ProjectAgentStatus status, DateTimeOffset occurredAtUtc) =>
        new()
        {
            ProjectId = _projectId,
            Kind = ProjectAgentLiveUpdateKind.AgentStatusChanged,
            OccurredAtUtc = occurredAtUtc,
            AgentStatus = new ProjectAgentStatusChangedDto
            {
                AgentId = agentId,
                Status = MapAgentStatus(status),
                OccurredAtUtc = occurredAtUtc,
            },
        };

    private ProjectAgentLiveUpdateDto CreateTimelineEntryUpsertUpdate(ProjectAgentTimelineEntryRecord entry) =>
        new()
        {
            ProjectId = _projectId,
            Kind = ProjectAgentLiveUpdateKind.TimelineEntryUpserted,
            OccurredAtUtc = entry.OccurredAtUtc,
            TimelineEntry = new ProjectAgentTimelineEntryDto
            {
                TimelineEntryId = entry.Id,
                AgentId = entry.ProjectAgentId,
                Sequence = entry.Sequence,
                EntryKind = MapTimelineEntryKind(entry.EntryType),
                OccurredAtUtc = entry.OccurredAtUtc,
                Message = entry.Message,
                ToolCallId = entry.ToolCallId,
                ToolName = entry.ToolName,
                ToolArguments = entry.ToolArguments,
                ToolResult = entry.ToolResult,
            },
        };

    private ProjectAgentLiveUpdateDto CreateTimelineEntriesRemovedUpdate(
        Guid agentId,
        IReadOnlyList<Guid> removedEntryIds,
        DateTimeOffset occurredAtUtc) =>
        new()
        {
            ProjectId = _projectId,
            Kind = ProjectAgentLiveUpdateKind.TimelineEntriesRemoved,
            OccurredAtUtc = occurredAtUtc,
            RemovedTimelineEntries = new ProjectAgentTimelineEntriesRemovedDto
            {
                AgentId = agentId,
                TimelineEntryIds = removedEntryIds,
            },
        };

    private static ProjectAgentRunStatus MapAgentStatus(Data.Entities.ProjectAgentStatus status) =>
        status switch
        {
            Data.Entities.ProjectAgentStatus.Waiting => ProjectAgentRunStatus.Waiting,
            Data.Entities.ProjectAgentStatus.Running => ProjectAgentRunStatus.Running,
            Data.Entities.ProjectAgentStatus.Completed => ProjectAgentRunStatus.Completed,
            Data.Entities.ProjectAgentStatus.Degraded => ProjectAgentRunStatus.Degraded,
            _ => throw new InvalidOperationException($"Unsupported persisted agent status '{status}'."),
        };

    private static ProjectAgentTimelineEntryKind MapTimelineEntryKind(ProjectAgentTimelineEntryType entryType) =>
        entryType switch
        {
            ProjectAgentTimelineEntryType.Input => ProjectAgentTimelineEntryKind.Input,
            ProjectAgentTimelineEntryType.Output => ProjectAgentTimelineEntryKind.Output,
            ProjectAgentTimelineEntryType.Tool => ProjectAgentTimelineEntryKind.Tool,
            ProjectAgentTimelineEntryType.Compaction => ProjectAgentTimelineEntryKind.Compaction,
            _ => throw new InvalidOperationException($"Unsupported persisted timeline entry type '{entryType}'."),
        };
}
