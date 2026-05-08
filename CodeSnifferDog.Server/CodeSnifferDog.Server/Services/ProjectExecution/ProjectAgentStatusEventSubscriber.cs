using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal sealed class ProjectAgentStatusEventSubscriber : IAsyncDisposable
{
    private readonly Guid _projectId;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory;
    private readonly IDisposable _subscription;
    private readonly object _sync = new();
    private Task _processingTail = Task.CompletedTask;
    private bool _disposed;

    public ProjectAgentStatusEventSubscriber(
        Guid projectId,
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        IObservable<AgentStatusEvent> events)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(events);

        _projectId = projectId;
        _dbContextFactory = dbContextFactory;
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

            case AgentCompactionEvent compactionEvent:
                await AppendCompactionEntryAsync(compactionEvent, cancellationToken).ConfigureAwait(false);
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
            return;
        }

        dbContext.ProjectAgentGroups.Add(new ProjectAgentGroupRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            RuntimeKey = agentEvent.GroupKey,
            DisplayName = agentEvent.DisplayName,
            CreatedAtUtc = agentEvent.OccurredAtUtc,
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
            existingAgent.Status = ParseStatus(agentEvent.InitialStatus);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        dbContext.ProjectAgents.Add(new ProjectAgentRecord
        {
            Id = Guid.NewGuid(),
            ProjectAgentGroupId = group.Id,
            RuntimeKey = agentEvent.AgentKey,
            DisplayName = agentEvent.DisplayName,
            Status = ParseStatus(agentEvent.InitialStatus),
            CreatedAtUtc = agentEvent.OccurredAtUtc,
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAgentStatusAsync(AgentStatusChangedEvent agentEvent, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, agentEvent.GroupKey, agentEvent.AgentKey, cancellationToken).ConfigureAwait(false);

        agent.Status = ParseStatus(agentEvent.Status);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        dbContext.ProjectAgentTimelineEntries.Add(new ProjectAgentTimelineEntryRecord
        {
            Id = Guid.NewGuid(),
            ProjectAgentId = agent.Id,
            Sequence = nextSequence,
            EntryType = entryType,
            Message = message,
            OccurredAtUtc = occurredAtUtc,
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    private static ProjectAgentStatus ParseStatus(string status) =>
        status.Trim() switch
        {
            "Waiting" => ProjectAgentStatus.Waiting,
            "Running" => ProjectAgentStatus.Running,
            "Completed" => ProjectAgentStatus.Completed,
            "Degraded" => ProjectAgentStatus.Degraded,
            _ => throw new InvalidOperationException($"Unsupported agent status '{status}'."),
        };
}
