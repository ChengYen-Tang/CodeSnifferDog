using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

internal sealed class AgentStatusProjectionMapper : IAgentStatusProjectionMapper
{
    public ProjectStatus MapProjectStatus(ProjectProcessingStatus status) => status switch
    {
        ProjectProcessingStatus.Queued => ProjectStatus.Queued,
        ProjectProcessingStatus.Reviewing => ProjectStatus.Reviewing,
        ProjectProcessingStatus.Completed => ProjectStatus.Completed,
        ProjectProcessingStatus.Failed => ProjectStatus.Failed,
        ProjectProcessingStatus.Canceled => ProjectStatus.Canceled,
        _ => throw new InvalidOperationException($"Unsupported project status '{status}'."),
    };

    public ProjectAgentRunStatus MapAgentStatus(PersistedAgentStatus status) => status switch
    {
        PersistedAgentStatus.Waiting => ProjectAgentRunStatus.Waiting,
        PersistedAgentStatus.Running => ProjectAgentRunStatus.Running,
        PersistedAgentStatus.Completed => ProjectAgentRunStatus.Completed,
        PersistedAgentStatus.Degraded => ProjectAgentRunStatus.Degraded,
        _ => throw new InvalidOperationException($"Unsupported persisted agent status '{status}'."),
    };

    public ProjectAgentTimelineEntryKind MapTimelineEntryKind(ProjectAgentTimelineEntryType entryType) => entryType switch
    {
        ProjectAgentTimelineEntryType.Input => ProjectAgentTimelineEntryKind.Input,
        ProjectAgentTimelineEntryType.Output => ProjectAgentTimelineEntryKind.Output,
        ProjectAgentTimelineEntryType.Tool => ProjectAgentTimelineEntryKind.Tool,
        ProjectAgentTimelineEntryType.Compaction => ProjectAgentTimelineEntryKind.Compaction,
        _ => throw new InvalidOperationException($"Unsupported persisted timeline entry type '{entryType}'."),
    };

    public ProjectAgentGroupLiveDto MapGroup(ProjectAgentGroupRecord group) => new()
    {
        GroupId = group.Id,
        RuntimeKey = group.RuntimeKey,
        DisplayName = group.DisplayName,
        CreatedAtUtc = group.CreatedAtUtc,
    };

    public ProjectAgentLiveDto MapAgent(ProjectAgentRecord agent) => new()
    {
        AgentId = agent.Id,
        GroupId = agent.ProjectAgentGroupId,
        RuntimeKey = agent.RuntimeKey,
        DisplayName = agent.DisplayName,
        SystemPrompt = agent.SystemPrompt,
        Status = MapAgentStatus(agent.Status),
        CreatedAtUtc = agent.CreatedAtUtc,
    };

    public ProjectAgentTimelineEntryDto MapTimelineEntry(ProjectAgentTimelineEntryRecord entry) => new()
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
    };
}
