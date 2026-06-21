using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;

internal sealed class ProjectAgentStatusLiveBackfillService(
    IProjectAgentStatusBackfillQueryService queryService,
    IAgentStatusProjectionMapper projectionMapper)
    : IProjectAgentStatusLiveBackfillService
{
    private readonly IProjectAgentStatusBackfillQueryService _queryService = queryService;
    private readonly IAgentStatusProjectionMapper _projectionMapper = projectionMapper;

    public async Task<IReadOnlyList<ProjectAgentLiveUpdateDto>> GetBackfillAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProjectAgentStatusBackfillReadModel backfill = await _queryService
            .GetBackfillAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (backfill.ProjectStatus is null)
            return [];

        List<ProjectAgentLiveUpdateDto> updates = [];
        updates.Add(new ProjectAgentLiveUpdateDto
        {
            ProjectId = backfill.ProjectId,
            Kind = ProjectAgentLiveUpdateKind.ProjectStatusChanged,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ProjectStatus = new ProjectExecutionStatusChangedDto
            {
                Status = _projectionMapper.MapProjectStatus(backfill.ProjectStatus.Value),
            },
        });

        updates.AddRange(backfill.Groups.Select(group => new ProjectAgentLiveUpdateDto
        {
            ProjectId = backfill.ProjectId,
            Kind = ProjectAgentLiveUpdateKind.AgentGroupUpserted,
            OccurredAtUtc = group.CreatedAtUtc,
            Group = _projectionMapper.MapGroup(group),
        }));

        updates.AddRange(backfill.Agents.Select(agent => new ProjectAgentLiveUpdateDto
        {
            ProjectId = backfill.ProjectId,
            Kind = ProjectAgentLiveUpdateKind.AgentUpserted,
            OccurredAtUtc = agent.CreatedAtUtc,
            Agent = _projectionMapper.MapAgent(agent, AgentStatusProjectionExceptionStyle.Snapshot),
        }));

        updates.AddRange(backfill.TimelineEntries.Select(entry => new ProjectAgentLiveUpdateDto
        {
            ProjectId = backfill.ProjectId,
            Kind = ProjectAgentLiveUpdateKind.TimelineEntryUpserted,
            OccurredAtUtc = entry.OccurredAtUtc,
            TimelineEntry = _projectionMapper.MapTimelineEntry(entry, AgentStatusProjectionExceptionStyle.Snapshot),
        }));

        return updates;
    }
}
