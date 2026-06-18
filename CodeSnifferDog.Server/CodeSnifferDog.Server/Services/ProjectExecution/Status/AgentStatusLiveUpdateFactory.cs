using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal sealed class AgentStatusLiveUpdateFactory
{
    public ProjectAgentLiveUpdateDto CreateGroupUpdate(Guid projectId, ProjectAgentGroupRecord group) =>
        new()
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.AgentGroupUpserted,
            OccurredAtUtc = group.CreatedAtUtc,
            Group = new ProjectAgentGroupLiveDto
            {
                GroupId = group.Id,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
            },
        };

    public ProjectAgentLiveUpdateDto CreateAgentUpsertUpdate(Guid projectId, ProjectAgentRecord agent) =>
        new()
        {
            ProjectId = projectId,
            Kind = ProjectAgentLiveUpdateKind.AgentUpserted,
            OccurredAtUtc = agent.CreatedAtUtc,
            Agent = new ProjectAgentLiveDto
            {
                AgentId = agent.Id,
                GroupId = agent.ProjectAgentGroupId,
                RuntimeKey = agent.RuntimeKey,
                DisplayName = agent.DisplayName,
                SystemPrompt = agent.SystemPrompt,
                Status = MapAgentStatus(agent.Status),
                CreatedAtUtc = agent.CreatedAtUtc,
            },
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
                Status = MapAgentStatus(status),
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
            TimelineEntry = new ProjectAgentTimelineEntryDto
            {
                TimelineEntryId = entry.Id,
                AgentId = entry.ProjectAgentId,
                Sequence = entry.Sequence,
                EntryKind = MapTimelineEntryKind(entry.EntryType),
                OccurredAtUtc = entry.OccurredAtUtc,
                Message = entry.Message,
                ToolCallId = entry.ToolCallId,
                ToolName = entry.ToolName,
                ToolArguments = entry.ToolArguments,
                ToolResult = entry.ToolResult,
            },
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

    internal static ProjectAgentRunStatus MapAgentStatus(Data.Entities.ProjectAgentStatus status) =>
        status switch
        {
            Data.Entities.ProjectAgentStatus.Waiting => ProjectAgentRunStatus.Waiting,
            Data.Entities.ProjectAgentStatus.Running => ProjectAgentRunStatus.Running,
            Data.Entities.ProjectAgentStatus.Completed => ProjectAgentRunStatus.Completed,
            Data.Entities.ProjectAgentStatus.Degraded => ProjectAgentRunStatus.Degraded,
            _ => throw new InvalidOperationException($"Unsupported persisted agent status '{status}'."),
        };

    internal static ProjectAgentTimelineEntryKind MapTimelineEntryKind(ProjectAgentTimelineEntryType entryType) =>
        entryType switch
        {
            ProjectAgentTimelineEntryType.Input => ProjectAgentTimelineEntryKind.Input,
            ProjectAgentTimelineEntryType.Output => ProjectAgentTimelineEntryKind.Output,
            ProjectAgentTimelineEntryType.Tool => ProjectAgentTimelineEntryKind.Tool,
            ProjectAgentTimelineEntryType.Compaction => ProjectAgentTimelineEntryKind.Compaction,
            _ => throw new InvalidOperationException($"Unsupported persisted timeline entry type '{entryType}'."),
        };
}
