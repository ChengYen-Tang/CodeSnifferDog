using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

internal sealed class LiveUpdateFactory(IProjectionMapper projectionMapper)
{
    private readonly IProjectionMapper _projectionMapper = projectionMapper;

    public LiveUpdateDto CreateGroupUpdate(Guid projectId, ProjectAgentGroupRecord group) =>
        new()
        {
            ProjectId = projectId,
            Kind = LiveUpdateKind.AgentGroupUpserted,
            OccurredAtUtc = group.CreatedAtUtc,
            Group = _projectionMapper.MapGroup(new GroupProjection(
                group.Id,
                group.RuntimeKey,
                group.DisplayName,
                group.CreatedAtUtc)),
        };

    public LiveUpdateDto CreateAgentUpsertUpdate(Guid projectId, ProjectAgentRecord agent) =>
        new()
        {
            ProjectId = projectId,
            Kind = LiveUpdateKind.AgentUpserted,
            OccurredAtUtc = agent.CreatedAtUtc,
            Agent = _projectionMapper.MapAgent(new AgentProjection(
                agent.Id,
                agent.ProjectAgentGroupId,
                agent.RuntimeKey,
                agent.DisplayName,
                agent.SystemPrompt,
                agent.Status,
                agent.CreatedAtUtc)),
        };

    public LiveUpdateDto CreateAgentStatusChangedUpdate(
        Guid projectId,
        Guid agentId,
        Data.Entities.ProjectAgentStatus status,
        DateTimeOffset occurredAtUtc) =>
        new()
        {
            ProjectId = projectId,
            Kind = LiveUpdateKind.AgentStatusChanged,
            OccurredAtUtc = occurredAtUtc,
            AgentStatus = new StatusChangedDto
            {
                AgentId = agentId,
                Status = _projectionMapper.MapAgentStatus(status),
                OccurredAtUtc = occurredAtUtc,
            },
        };

    public LiveUpdateDto CreateTimelineEntryUpsertUpdate(
        Guid projectId,
        ProjectAgentTimelineEntryRecord entry) =>
        new()
        {
            ProjectId = projectId,
            Kind = LiveUpdateKind.TimelineEntryUpserted,
            OccurredAtUtc = entry.OccurredAtUtc,
            TimelineEntry = _projectionMapper.MapTimelineEntry(new TimelineEntryProjection(
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

    public LiveUpdateDto CreateTimelineEntriesRemovedUpdate(
        Guid projectId,
        Guid agentId,
        IReadOnlyList<Guid> removedEntryIds,
        DateTimeOffset occurredAtUtc) =>
        new()
        {
            ProjectId = projectId,
            Kind = LiveUpdateKind.TimelineEntriesRemoved,
            OccurredAtUtc = occurredAtUtc,
            RemovedTimelineEntries = new TimelineEntriesRemovedDto
            {
                AgentId = agentId,
                TimelineEntryIds = removedEntryIds,
            },
        };
}
