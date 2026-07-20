using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;

/// <summary>
/// Builds status and history snapshots from persisted query read models.
/// </summary>
/// <param name="queryService">Query service that loads persisted snapshot read models.</param>
/// <param name="projectionMapper">Mapper that converts persisted projections to shared DTOs.</param>
internal sealed class SnapshotService(
    ISnapshotQueryService queryService,
    IProjectionMapper projectionMapper)
    : ISnapshotService
{
    private readonly ISnapshotQueryService _queryService = queryService;
    private readonly IProjectionMapper _projectionMapper = projectionMapper;

    /// <inheritdoc />
    public async Task<StatusSnapshotDto?> GetSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId = null,
        CancellationToken cancellationToken = default)
    {
        SnapshotReadModel? snapshot = await _queryService
            .GetSnapshotAsync(projectId, selectedAgentId, cancellationToken)
            .ConfigureAwait(false);

        if (snapshot is null)
            return null;

        return new StatusSnapshotDto
        {
            ProjectId = snapshot.ProjectId,
            ProjectStatus = _projectionMapper.MapProjectStatus(snapshot.ProjectStatus),
            SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
            AgentGroups = snapshot.Groups
                .Select(MapGroup)
            .ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<HistorySnapshotDto?> GetAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        HistorySnapshotReadModel? history = await _queryService
            .GetAgentHistoryAsync(projectId, agentId, cancellationToken)
            .ConfigureAwait(false);

        if (history is null)
            return null;

        IReadOnlyList<TimelineEntryDto> timelineEntries = history.TimelineEntries
            .Select(entry => _projectionMapper.MapTimelineEntry(entry, ExceptionStyle.Snapshot))
            .ToList();

        return new HistorySnapshotDto
        {
            ProjectId = history.ProjectId,
            AgentId = history.AgentId,
            SystemPrompt = history.SystemPrompt,
            TimelineEntries = timelineEntries,
        };
    }

    /// <summary>
    /// Maps one snapshot group row to the shared group snapshot DTO.
    /// </summary>
    /// <param name="group">Persisted snapshot group row.</param>
    /// <returns>The mapped group snapshot DTO.</returns>
    private GroupSnapshotDto MapGroup(SnapshotGroupRow group) => new()
    {
        GroupId = group.Group.GroupId,
        RuntimeKey = group.Group.RuntimeKey,
        DisplayName = group.Group.DisplayName,
        CreatedAtUtc = group.Group.CreatedAtUtc,
        Agents = group.Agents.Select(MapAgent).ToList(),
    };

    /// <summary>
    /// Maps one snapshot agent row to the shared agent snapshot DTO.
    /// </summary>
    /// <param name="agent">Persisted snapshot agent row.</param>
    /// <returns>The mapped agent snapshot DTO.</returns>
    private SnapshotDto MapAgent(SnapshotAgentRow agent) => new()
    {
        AgentId = agent.Agent.AgentId,
        GroupId = agent.Agent.GroupId,
        RuntimeKey = agent.Agent.RuntimeKey,
        DisplayName = agent.Agent.DisplayName,
        SystemPrompt = agent.SystemPrompt,
        Status = _projectionMapper.MapAgentStatus(agent.Agent.Status, ExceptionStyle.Snapshot),
        CreatedAtUtc = agent.Agent.CreatedAtUtc,
        HasLoadedHistory = agent.HasLoadedHistory,
        TimelineEntries = agent.TimelineEntries
            .Select(entry => _projectionMapper.MapTimelineEntry(entry, ExceptionStyle.Snapshot))
            .ToList(),
    };
}
