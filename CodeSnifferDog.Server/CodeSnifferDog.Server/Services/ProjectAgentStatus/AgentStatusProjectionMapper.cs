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

    public ProjectAgentRunStatus MapAgentStatus(
        PersistedAgentStatus status,
        AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted) => status switch
    {
        PersistedAgentStatus.Waiting => ProjectAgentRunStatus.Waiting,
        PersistedAgentStatus.Running => ProjectAgentRunStatus.Running,
        PersistedAgentStatus.Completed => ProjectAgentRunStatus.Completed,
        PersistedAgentStatus.Degraded => ProjectAgentRunStatus.Degraded,
        _ => throw new InvalidOperationException(exceptionStyle switch
        {
            AgentStatusProjectionExceptionStyle.Snapshot => $"Unsupported agent status '{status}'.",
            _ => $"Unsupported persisted agent status '{status}'.",
        }),
    };

    public ProjectAgentTimelineEntryKind MapTimelineEntryKind(
        ProjectAgentTimelineEntryType entryType,
        AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted) => entryType switch
    {
        ProjectAgentTimelineEntryType.Input => ProjectAgentTimelineEntryKind.Input,
        ProjectAgentTimelineEntryType.Output => ProjectAgentTimelineEntryKind.Output,
        ProjectAgentTimelineEntryType.Tool => ProjectAgentTimelineEntryKind.Tool,
        ProjectAgentTimelineEntryType.Compaction => ProjectAgentTimelineEntryKind.Compaction,
        _ => throw new InvalidOperationException(exceptionStyle switch
        {
            AgentStatusProjectionExceptionStyle.Snapshot => $"Unsupported timeline entry type '{entryType}'.",
            _ => $"Unsupported persisted timeline entry type '{entryType}'.",
        }),
    };

    public ProjectAgentGroupLiveDto MapGroup(AgentStatusGroupProjection group) => new()
    {
        GroupId = group.GroupId,
        RuntimeKey = group.RuntimeKey,
        DisplayName = group.DisplayName,
        CreatedAtUtc = group.CreatedAtUtc,
    };

    public ProjectAgentLiveDto MapAgent(
        AgentStatusAgentProjection agent,
        AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted) => new()
    {
        AgentId = agent.AgentId,
        GroupId = agent.GroupId,
        RuntimeKey = agent.RuntimeKey,
        DisplayName = agent.DisplayName,
        SystemPrompt = agent.SystemPrompt,
        Status = MapAgentStatus(agent.Status, exceptionStyle),
        CreatedAtUtc = agent.CreatedAtUtc,
    };

    public ProjectAgentTimelineEntryDto MapTimelineEntry(
        AgentStatusTimelineEntryProjection entry,
        AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted) => new()
    {
        TimelineEntryId = entry.TimelineEntryId,
        AgentId = entry.AgentId,
        Sequence = entry.Sequence,
        EntryKind = MapTimelineEntryKind(entry.EntryType, exceptionStyle),
        OccurredAtUtc = entry.OccurredAtUtc,
        Message = entry.Message,
        ToolCallId = entry.ToolCallId,
        ToolName = entry.ToolName,
        ToolArguments = entry.ToolArguments,
        ToolResult = entry.ToolResult,
    };
}
