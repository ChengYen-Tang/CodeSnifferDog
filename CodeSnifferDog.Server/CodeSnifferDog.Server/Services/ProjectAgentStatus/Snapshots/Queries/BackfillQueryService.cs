using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

/// <summary>
/// Loads persisted rows required to backfill newly connected live-update clients.
/// </summary>
/// <param name="dbContextFactory">Factory used to create database contexts for read queries.</param>
internal sealed class BackfillQueryService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory)
    : IBackfillQueryService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    /// <inheritdoc />
    public async Task<BackfillReadModel> GetBackfillAsync(
        LiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (!request.IncludeProjectState)
        {
            IReadOnlyList<TimelineEntryProjection> selectedTimeline =
                request.AgentId is Guid selectedAgentId &&
                await AgentBelongsToProjectAsync(
                    dbContext,
                    request.ProjectId,
                    selectedAgentId,
                    cancellationToken).ConfigureAwait(false)
                    ? await LoadTimelineEntriesAsync(
                        dbContext,
                        selectedAgentId,
                        request.LatestSequence,
                        cancellationToken).ConfigureAwait(false)
                    : [];

            return new BackfillReadModel(
                request.ProjectId,
                null,
                [],
                [],
                selectedTimeline);
        }

        ProjectProcessingStatus? projectStatus = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == request.ProjectId)
            .Select(project => (ProjectProcessingStatus?)project.Status)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (projectStatus is null)
            return new BackfillReadModel(request.ProjectId, null, [], [], []);

        List<GroupProjection> groups = await dbContext.ProjectAgentGroups
            .AsNoTracking()
            .Where(group => group.ProjectId == request.ProjectId)
            .OrderBy(group => group.CreatedAtUtc)
            .ThenBy(group => group.DisplayName)
            .Select(group => new GroupProjection(
                group.Id,
                group.RuntimeKey,
                group.DisplayName,
                group.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Guid> groupIds = groups.Select(group => group.GroupId).ToList();
        List<AgentProjection> agents = await dbContext.ProjectAgents
            .AsNoTracking()
            .Where(agent => groupIds.Contains(agent.ProjectAgentGroupId))
            .OrderBy(agent => agent.CreatedAtUtc)
            .ThenBy(agent => agent.DisplayName)
            .Select(agent => new AgentProjection(
                agent.Id,
                agent.ProjectAgentGroupId,
                agent.RuntimeKey,
                agent.DisplayName,
                null,
                agent.Status,
                agent.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<TimelineEntryProjection> timelineEntries =
            request.AgentId is Guid agentId && agents.Any(agent => agent.AgentId == agentId)
            ? await LoadTimelineEntriesAsync(dbContext, agentId, request.LatestSequence, cancellationToken).ConfigureAwait(false)
            : [];

        return new BackfillReadModel(
            request.ProjectId,
            projectStatus,
            groups,
            agents,
            timelineEntries);
    }

    /// <summary>
    /// Loads timeline entries newer than the client's latest known sequence for one agent.
    /// </summary>
    /// <param name="dbContext">Database context used for the query.</param>
    /// <param name="agentId">Agent identifier whose timeline should be loaded.</param>
    /// <param name="latestSequence">Latest sequence the client already knows.</param>
    /// <param name="cancellationToken">Cancels query execution.</param>
    /// <returns>The matching timeline-entry projections ordered by sequence.</returns>
    private static Task<List<TimelineEntryProjection>> LoadTimelineEntriesAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        long latestSequence,
        CancellationToken cancellationToken) =>
        dbContext.ProjectAgentTimelineEntries
            .AsNoTracking()
            .Where(entry => entry.ProjectAgentId == agentId && entry.Sequence > latestSequence)
            .OrderBy(entry => entry.Sequence)
            .Select(entry => new TimelineEntryProjection(
                entry.Id,
                entry.ProjectAgentId,
                entry.Sequence,
                entry.EntryType,
                entry.OccurredAtUtc,
                entry.Message,
                entry.ToolCallId,
                entry.ToolName,
                entry.ToolArguments,
                entry.ToolResult))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Checks that an agent belongs to the requested project without loading the full roster.
    /// </summary>
    private static Task<bool> AgentBelongsToProjectAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken) =>
        dbContext.ProjectAgents
            .AsNoTracking()
            .Where(agent => agent.Id == agentId)
            .Join(
                dbContext.ProjectAgentGroups
                    .AsNoTracking()
                    .Where(group => group.ProjectId == projectId),
                agent => agent.ProjectAgentGroupId,
                group => group.Id,
                (agent, _) => agent.Id)
            .AnyAsync(candidateAgentId => candidateAgentId == agentId, cancellationToken);
}
