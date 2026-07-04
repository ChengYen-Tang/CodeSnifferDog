using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using ILiveUpdateNotifier = CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications.ILiveUpdateNotifier;
using Microsoft.EntityFrameworkCore;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

/// <summary>
/// Persists agent groups, agents, and timeline events for a project's execution status stream.
/// </summary>
internal sealed class PersistenceService(
    Guid projectId,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    ILiveUpdateNotifier liveUpdateNotifier,
    LiveUpdateFactory liveUpdateFactory,
    ITimelinePersistenceService timelinePersistenceService) : IPersistenceService
{
    private readonly Guid _projectId = projectId;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILiveUpdateNotifier _liveUpdateNotifier = liveUpdateNotifier;
    private readonly LiveUpdateFactory _liveUpdateFactory = liveUpdateFactory;
    private readonly ITimelinePersistenceService _timelinePersistenceService = timelinePersistenceService;

    /// <inheritdoc />
    public async Task UpsertGroupAsync(GroupCreatedEvent agentEvent, CancellationToken cancellationToken)
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

    /// <inheritdoc />
    public async Task UpsertAgentAsync(CreatedEvent agentEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agentEvent.GroupKey))
            throw new InvalidOperationException("CreatedEvent.GroupKey is required for persistence.");

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

    /// <inheritdoc />
    public async Task UpdateAgentStatusAsync(StatusChangedEvent agentEvent, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, agentEvent.GroupKey, agentEvent.AgentKey, cancellationToken).ConfigureAwait(false);

        agent.Status = ParseStatus(agentEvent.Status);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateAgentStatusChangedUpdate(_projectId, agent.Id, agent.Status, agentEvent.OccurredAtUtc), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveTranscriptEntriesAsync(
        TranscriptClearedEvent agentEvent,
        CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, agentEvent.GroupKey, agentEvent.AgentKey, cancellationToken).ConfigureAwait(false);
        TimelineRemovalMutationResult? result = await _timelinePersistenceService.RemoveTranscriptEntriesAsync(
            dbContext,
            agent.Id,
            agentEvent,
            cancellationToken).ConfigureAwait(false);

        if (result is null)
            return;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(
            _liveUpdateFactory.CreateTimelineEntriesRemovedUpdate(
                _projectId,
                result.AgentId,
                result.RemovedEntryIds,
                result.OccurredAtUtc),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AppendToolCallStartedEntryAsync(ToolCallStartedEvent agentEvent, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, agentEvent.GroupKey, agentEvent.AgentKey, cancellationToken).ConfigureAwait(false);
        TimelineEntryMutationResult result = await _timelinePersistenceService.AppendToolCallStartedEntryAsync(
            dbContext,
            agent.Id,
            agentEvent,
            cancellationToken).ConfigureAwait(false);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateTimelineEntryUpsertUpdate(_projectId, result.Entry), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CompleteToolCallEntryAsync(ToolCallCompletedEvent agentEvent, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectAgentRecord agent = await GetAgentAsync(dbContext, agentEvent.GroupKey, agentEvent.AgentKey, cancellationToken).ConfigureAwait(false);
        TimelineEntryMutationResult result = await _timelinePersistenceService.CompleteToolCallEntryAsync(
            dbContext,
            agent.Id,
            agentEvent,
            cancellationToken).ConfigureAwait(false);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateTimelineEntryUpsertUpdate(_projectId, result.Entry), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
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
        TimelineEntryMutationResult result = await _timelinePersistenceService.AppendTimelineEntryAsync(
            dbContext,
            agent.Id,
            entryType,
            message,
            occurredAtUtc,
            cancellationToken).ConfigureAwait(false);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await NotifyAsync(_liveUpdateFactory.CreateTimelineEntryUpsertUpdate(_projectId, result.Entry), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the persisted agent record for a runtime group and agent key pair.
    /// </summary>
    /// <param name="dbContext">Database context used for persistence.</param>
    /// <param name="groupKey">Runtime key of the agent group.</param>
    /// <param name="agentKey">Runtime key of the agent.</param>
    /// <param name="cancellationToken">Token that cancels the database operation.</param>
    /// <returns>The matching persisted agent record.</returns>
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

    /// <summary>
    /// Parses a runtime status string into the persisted agent status enum.
    /// </summary>
    /// <param name="status">Runtime status string.</param>
    /// <returns>The persisted agent status.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="status"/> is not supported.</exception>
    internal static Data.Entities.ProjectAgentStatus ParseStatus(string status) =>
        status.Trim() switch
        {
            "Waiting" => Data.Entities.ProjectAgentStatus.Waiting,
            "Running" => Data.Entities.ProjectAgentStatus.Running,
            "Completed" => Data.Entities.ProjectAgentStatus.Completed,
            "Degraded" => Data.Entities.ProjectAgentStatus.Degraded,
            _ => throw new InvalidOperationException($"Unsupported agent status '{status}'."),
        };

    /// <summary>
    /// Publishes a live update for a persisted mutation.
    /// </summary>
    /// <param name="update">Live update payload to publish.</param>
    /// <param name="cancellationToken">Token that cancels publishing.</param>
    private Task NotifyAsync(LiveUpdateDto update, CancellationToken cancellationToken) =>
        _liveUpdateNotifier.NotifyAsync(update, cancellationToken);
}
