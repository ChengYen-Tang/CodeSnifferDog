using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

/// <summary>
/// Creates live-update payloads from persisted project-agent records.
/// </summary>
internal sealed class LiveUpdateFactory(IProjectionMapper projectionMapper)
{
    private readonly IProjectionMapper _projectionMapper = projectionMapper;

    /// <summary>
    /// Creates a live update for an agent group insert or update.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the group.</param>
    /// <param name="group">Persisted agent group record.</param>
    /// <returns>The live update payload.</returns>
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

    /// <summary>
    /// Creates a live update for an agent insert or update.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the agent.</param>
    /// <param name="agent">Persisted agent record.</param>
    /// <returns>The live update payload.</returns>
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

    /// <summary>
    /// Creates a live update for an agent status transition.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the agent.</param>
    /// <param name="agentId">Agent identifier whose status changed.</param>
    /// <param name="status">Persisted status value.</param>
    /// <param name="occurredAtUtc">Timestamp for the status change.</param>
    /// <returns>The live update payload.</returns>
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

    /// <summary>
    /// Creates a live update for a timeline entry insert or update.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the timeline entry.</param>
    /// <param name="entry">Persisted timeline entry record.</param>
    /// <returns>The live update payload.</returns>
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

    /// <summary>
    /// Creates a live update for removed timeline entries.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the timeline entries.</param>
    /// <param name="agentId">Agent identifier whose entries were removed.</param>
    /// <param name="removedEntryIds">Identifiers of removed timeline entries.</param>
    /// <param name="occurredAtUtc">Timestamp for the removal.</param>
    /// <returns>The live update payload.</returns>
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
