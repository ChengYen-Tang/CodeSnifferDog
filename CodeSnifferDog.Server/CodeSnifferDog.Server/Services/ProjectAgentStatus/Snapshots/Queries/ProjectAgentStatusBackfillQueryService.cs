using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

internal sealed class ProjectAgentStatusBackfillQueryService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory)
    : IProjectAgentStatusBackfillQueryService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    public async Task<ProjectAgentStatusBackfillReadModel> GetBackfillAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectProcessingStatus? projectStatus = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == request.ProjectId)
            .Select(project => (ProjectProcessingStatus?)project.Status)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (projectStatus is null)
            return new ProjectAgentStatusBackfillReadModel(request.ProjectId, null, [], [], []);

        List<AgentStatusGroupProjection> groups = await dbContext.ProjectAgentGroups
            .AsNoTracking()
            .Where(group => group.ProjectId == request.ProjectId)
            .OrderBy(group => group.CreatedAtUtc)
            .ThenBy(group => group.DisplayName)
            .Select(group => new AgentStatusGroupProjection(
                group.Id,
                group.RuntimeKey,
                group.DisplayName,
                group.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Guid> groupIds = groups.Select(group => group.GroupId).ToList();
        List<AgentStatusAgentProjection> agents = await dbContext.ProjectAgents
            .AsNoTracking()
            .Where(agent => groupIds.Contains(agent.ProjectAgentGroupId))
            .OrderBy(agent => agent.CreatedAtUtc)
            .ThenBy(agent => agent.DisplayName)
            .Select(agent => new AgentStatusAgentProjection(
                agent.Id,
                agent.ProjectAgentGroupId,
                agent.RuntimeKey,
                agent.DisplayName,
                agent.SystemPrompt,
                agent.Status,
                agent.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AgentStatusTimelineEntryProjection> timelineEntries =
            request.AgentId is Guid agentId && agents.Any(agent => agent.AgentId == agentId)
            ? await LoadTimelineEntriesAsync(dbContext, agentId, request.LatestSequence, cancellationToken).ConfigureAwait(false)
            : [];

        return new ProjectAgentStatusBackfillReadModel(
            request.ProjectId,
            projectStatus,
            groups,
            agents,
            timelineEntries);
    }

    private static Task<List<AgentStatusTimelineEntryProjection>> LoadTimelineEntriesAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        long latestSequence,
        CancellationToken cancellationToken) =>
        dbContext.ProjectAgentTimelineEntries
            .AsNoTracking()
            .Where(entry => entry.ProjectAgentId == agentId && entry.Sequence > latestSequence)
            .OrderBy(entry => entry.Sequence)
            .Select(entry => new AgentStatusTimelineEntryProjection(
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
}
