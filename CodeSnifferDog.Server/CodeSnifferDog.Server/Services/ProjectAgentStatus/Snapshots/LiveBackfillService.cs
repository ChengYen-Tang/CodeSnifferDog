using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;

/// <summary>
/// Builds live-update backfill sequences from persisted snapshot projections.
/// </summary>
/// <param name="queryService">Query service that loads persisted backfill read models.</param>
/// <param name="projectionMapper">Mapper that converts persisted projections to shared DTOs.</param>
internal sealed class LiveBackfillService(
    IBackfillQueryService queryService,
    IProjectionMapper projectionMapper)
    : ILiveBackfillService
{
    private readonly IBackfillQueryService _queryService = queryService;
    private readonly IProjectionMapper _projectionMapper = projectionMapper;

    /// <inheritdoc />
    public async Task<IReadOnlyList<LiveUpdateDto>> GetBackfillAsync(
        LiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        BackfillReadModel backfill = await _queryService
            .GetBackfillAsync(request, cancellationToken)
            .ConfigureAwait(false);

        List<LiveUpdateDto> updates = [];
        if (backfill.ProjectStatus is { } projectStatus)
        {
            updates.Add(new LiveUpdateDto
            {
                ProjectId = backfill.ProjectId,
                Kind = LiveUpdateKind.ProjectStatusChanged,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                ProjectStatus = new ExecutionStatusChangedDto
                {
                    Status = _projectionMapper.MapProjectStatus(projectStatus),
                },
            });

            updates.AddRange(backfill.Groups.Select(group => new LiveUpdateDto
            {
                ProjectId = backfill.ProjectId,
                Kind = LiveUpdateKind.AgentGroupUpserted,
                OccurredAtUtc = group.CreatedAtUtc,
                Group = _projectionMapper.MapGroup(group),
            }));

            updates.AddRange(backfill.Agents.Select(agent => new LiveUpdateDto
            {
                ProjectId = backfill.ProjectId,
                Kind = LiveUpdateKind.AgentUpserted,
                OccurredAtUtc = agent.CreatedAtUtc,
                Agent = _projectionMapper.MapAgent(agent, ExceptionStyle.Snapshot),
            }));
        }

        updates.AddRange(backfill.TimelineEntries.Select(entry => new LiveUpdateDto
        {
            ProjectId = backfill.ProjectId,
            Kind = LiveUpdateKind.TimelineEntryUpserted,
            OccurredAtUtc = entry.OccurredAtUtc,
            TimelineEntry = _projectionMapper.MapTimelineEntry(entry, ExceptionStyle.Snapshot),
        }));

        return updates;
    }
}
