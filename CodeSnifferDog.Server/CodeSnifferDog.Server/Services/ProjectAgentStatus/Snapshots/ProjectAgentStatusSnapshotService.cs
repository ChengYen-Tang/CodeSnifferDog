using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;

internal sealed class ProjectAgentStatusSnapshotService(
    IProjectAgentStatusSnapshotQueryService queryService,
    IAgentStatusProjectionMapper projectionMapper)
    : IProjectAgentStatusSnapshotService
{
    private readonly IProjectAgentStatusSnapshotQueryService _queryService = queryService;
    private readonly IAgentStatusProjectionMapper _projectionMapper = projectionMapper;

    public async Task<ProjectAgentStatusSnapshotDto?> GetSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ProjectAgentStatusSnapshotReadModel? snapshot = await _queryService
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
        ProjectAgentHistorySnapshotReadModel? history = await _queryService
            .GetAgentHistoryAsync(projectId, agentId, cancellationToken)
            .ConfigureAwait(false);

        if (history is null)
            return null;

        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries = history.TimelineEntries
            .Select(entry => _projectionMapper.MapTimelineEntry(entry, AgentStatusProjectionExceptionStyle.Snapshot))
            .ToList();

        return new ProjectAgentHistorySnapshotDto
        {
            ProjectId = history.ProjectId,
            AgentId = history.AgentId,
            TimelineEntries = timelineEntries,
        };
    }

    private ProjectAgentGroupSnapshotDto MapGroup(ProjectAgentStatusSnapshotGroupRow group) => new()
    {
        GroupId = group.Group.GroupId,
        RuntimeKey = group.Group.RuntimeKey,
        DisplayName = group.Group.DisplayName,
        CreatedAtUtc = group.Group.CreatedAtUtc,
        Agents = group.Agents.Select(MapAgent).ToList(),
    };

    private ProjectAgentSnapshotDto MapAgent(ProjectAgentStatusSnapshotAgentRow agent) => new()
    {
        AgentId = agent.Agent.AgentId,
        GroupId = agent.Agent.GroupId,
        RuntimeKey = agent.Agent.RuntimeKey,
        DisplayName = agent.Agent.DisplayName,
        SystemPrompt = agent.Agent.SystemPrompt,
        Status = _projectionMapper.MapAgentStatus(agent.Agent.Status, AgentStatusProjectionExceptionStyle.Snapshot),
        CreatedAtUtc = agent.Agent.CreatedAtUtc,
        HasLoadedHistory = agent.HasLoadedHistory,
        TimelineEntries = agent.TimelineEntries
            .Select(entry => _projectionMapper.MapTimelineEntry(entry, AgentStatusProjectionExceptionStyle.Snapshot))
            .ToList(),
    };
}
