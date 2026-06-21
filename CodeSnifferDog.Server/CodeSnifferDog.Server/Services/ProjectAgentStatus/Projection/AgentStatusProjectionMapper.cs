using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

internal sealed class AgentStatusProjectionMapper(IProjectStatusMapper projectStatusMapper) : IAgentStatusProjectionMapper
{
    private readonly IProjectStatusMapper _projectStatusMapper = projectStatusMapper;

    public ProjectStatus MapProjectStatus(ProjectProcessingStatus status) =>
        _projectStatusMapper.Map(status, ProjectStatusMappingExceptionStyle.Persisted);

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
