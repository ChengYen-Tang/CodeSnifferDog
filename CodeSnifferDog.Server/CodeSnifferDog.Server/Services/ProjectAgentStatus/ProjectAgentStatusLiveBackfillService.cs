using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

internal sealed class ProjectAgentStatusLiveBackfillService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IAgentStatusProjectionMapper projectionMapper)
    : IProjectAgentStatusLiveBackfillService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IAgentStatusProjectionMapper _projectionMapper = projectionMapper;

    public async Task<IReadOnlyList<ProjectAgentLiveUpdateDto>> GetBackfillAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectProcessingStatus? projectStatus = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == request.ProjectId)
            .Select(project => (ProjectProcessingStatus?)project.Status)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (projectStatus is null)
            return [];

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

        List<ProjectAgentLiveUpdateDto> updates = [];
        updates.Add(new ProjectAgentLiveUpdateDto
        {
            ProjectId = request.ProjectId,
            Kind = ProjectAgentLiveUpdateKind.ProjectStatusChanged,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ProjectStatus = new ProjectExecutionStatusChangedDto
            {
                Status = _projectionMapper.MapProjectStatus(projectStatus.Value),
            },
        });

        updates.AddRange(groups.Select(group => new ProjectAgentLiveUpdateDto
        {
            ProjectId = request.ProjectId,
            Kind = ProjectAgentLiveUpdateKind.AgentGroupUpserted,
            OccurredAtUtc = group.CreatedAtUtc,
            Group = _projectionMapper.MapGroup(group),
        }));

        updates.AddRange(agents.Select(agent => new ProjectAgentLiveUpdateDto
        {
            ProjectId = request.ProjectId,
            Kind = ProjectAgentLiveUpdateKind.AgentUpserted,
            OccurredAtUtc = agent.CreatedAtUtc,
            Agent = _projectionMapper.MapAgent(agent, AgentStatusProjectionExceptionStyle.Snapshot),
        }));

        if (request.AgentId is Guid agentId)
        {
            List<AgentStatusTimelineEntryProjection> timelineEntries = await dbContext.ProjectAgentTimelineEntries
                .AsNoTracking()
                .Where(entry => entry.ProjectAgentId == agentId && entry.Sequence > request.LatestSequence)
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
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            updates.AddRange(timelineEntries.Select(entry => new ProjectAgentLiveUpdateDto
            {
                ProjectId = request.ProjectId,
                Kind = ProjectAgentLiveUpdateKind.TimelineEntryUpserted,
                OccurredAtUtc = entry.OccurredAtUtc,
                TimelineEntry = _projectionMapper.MapTimelineEntry(entry, AgentStatusProjectionExceptionStyle.Snapshot),
            }));
        }

        return updates;
    }
}
