using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;

internal sealed class SnapshotService(
    ISnapshotQueryService queryService,
    IProjectionMapper projectionMapper)
    : ISnapshotService
{
    private readonly ISnapshotQueryService _queryService = queryService;
    private readonly IProjectionMapper _projectionMapper = projectionMapper;

    public async Task<ProjectAgentStatusSnapshotDto?> GetSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId = null,
        CancellationToken cancellationToken = default)
    {
        SnapshotReadModel? snapshot = await _queryService
            .GetSnapshotAsync(projectId, selectedAgentId, cancellationToken)
            .ConfigureAwait(false);

        if (snapshot is null)
            return null;

        return new ProjectAgentStatusSnapshotDto
        {
            ProjectId = snapshot.ProjectId,
            ProjectStatus = _projectionMapper.MapProjectStatus(snapshot.ProjectStatus),
            SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
            AgentGroups = snapshot.Groups
                .Select(MapGroup)
                .ToList(),
        };
    }

    public async Task<ProjectAgentHistorySnapshotDto?> GetAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        HistorySnapshotReadModel? history = await _queryService
            .GetAgentHistoryAsync(projectId, agentId, cancellationToken)
            .ConfigureAwait(false);

        if (history is null)
            return null;

        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries = history.TimelineEntries
            .Select(entry => _projectionMapper.MapTimelineEntry(entry, ExceptionStyle.Snapshot))
            .ToList();

        return new ProjectAgentHistorySnapshotDto
        {
            ProjectId = history.ProjectId,
            AgentId = history.AgentId,
            TimelineEntries = timelineEntries,
        };
    }

    private ProjectAgentGroupSnapshotDto MapGroup(SnapshotGroupRow group) => new()
    {
        GroupId = group.Group.GroupId,
        RuntimeKey = group.Group.RuntimeKey,
        DisplayName = group.Group.DisplayName,
        CreatedAtUtc = group.Group.CreatedAtUtc,
        Agents = group.Agents.Select(MapAgent).ToList(),
    };

    private ProjectAgentSnapshotDto MapAgent(SnapshotAgentRow agent) => new()
    {
        AgentId = agent.Agent.AgentId,
        GroupId = agent.Agent.GroupId,
        RuntimeKey = agent.Agent.RuntimeKey,
        DisplayName = agent.Agent.DisplayName,
        SystemPrompt = agent.Agent.SystemPrompt,
        Status = _projectionMapper.MapAgentStatus(agent.Agent.Status, ExceptionStyle.Snapshot),
        CreatedAtUtc = agent.Agent.CreatedAtUtc,
        HasLoadedHistory = agent.HasLoadedHistory,
        TimelineEntries = agent.TimelineEntries
            .Select(entry => _projectionMapper.MapTimelineEntry(entry, ExceptionStyle.Snapshot))
            .ToList(),
    };
}
