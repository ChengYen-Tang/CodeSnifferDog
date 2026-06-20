using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

internal sealed class AgentStatusLiveUpdateFactory(IAgentStatusProjectionMapper projectionMapper)
{
    private readonly IAgentStatusProjectionMapper _projectionMapper = projectionMapper;

    public ProjectAgentLiveUpdateDto CreateGroupUpdate(Guid projectId, ProjectAgentGroupRecord group) =>
        new()
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.AgentGroupUpserted,
            OccurredAtUtc = group.CreatedAtUtc,
            Group = _projectionMapper.MapGroup(new AgentStatusGroupProjection(
                group.Id,
                group.RuntimeKey,
                group.DisplayName,
                group.CreatedAtUtc)),
        };

    public ProjectAgentLiveUpdateDto CreateAgentUpsertUpdate(Guid projectId, ProjectAgentRecord agent) =>
        new()
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.AgentUpserted,
            OccurredAtUtc = agent.CreatedAtUtc,
            Agent = _projectionMapper.MapAgent(new AgentStatusAgentProjection(
                agent.Id,
                agent.ProjectAgentGroupId,
                agent.RuntimeKey,
                agent.DisplayName,
                agent.SystemPrompt,
                agent.Status,
                agent.CreatedAtUtc)),
        };

    public ProjectAgentLiveUpdateDto CreateAgentStatusChangedUpdate(
        Guid projectId,
        Guid agentId,
        Data.Entities.ProjectAgentStatus status,
        DateTimeOffset occurredAtUtc) =>
        new()
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.AgentStatusChanged,
            OccurredAtUtc = occurredAtUtc,
            AgentStatus = new ProjectAgentStatusChangedDto
            {
                AgentId = agentId,
                Status = _projectionMapper.MapAgentStatus(status),
                OccurredAtUtc = occurredAtUtc,
            },
        };

    public ProjectAgentLiveUpdateDto CreateTimelineEntryUpsertUpdate(
        Guid projectId,
        ProjectAgentTimelineEntryRecord entry) =>
        new()
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.TimelineEntryUpserted,
            OccurredAtUtc = entry.OccurredAtUtc,
            TimelineEntry = _projectionMapper.MapTimelineEntry(new AgentStatusTimelineEntryProjection(
                entry.Id,
                entry.ProjectAgentId,
                entry.Sequence,
                entry.EntryType,
                entry.OccurredAtUtc,
                entry.Message,
                entry.ToolCallId,
                entry.ToolName,
                entry.ToolArguments,
                entry.ToolResult)),
        };

    public ProjectAgentLiveUpdateDto CreateTimelineEntriesRemovedUpdate(
        Guid projectId,
        Guid agentId,
        IReadOnlyList<Guid> removedEntryIds,
        DateTimeOffset occurredAtUtc) =>
        new()
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.TimelineEntriesRemoved,
            OccurredAtUtc = occurredAtUtc,
            RemovedTimelineEntries = new ProjectAgentTimelineEntriesRemovedDto
            {
                AgentId = agentId,
                TimelineEntryIds = removedEntryIds,
            },
        };
}
