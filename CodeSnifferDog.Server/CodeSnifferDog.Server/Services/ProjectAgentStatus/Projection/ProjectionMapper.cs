using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

/// <summary>
/// Maps persisted agent-status projections to shared snapshot and live-update DTOs.
/// </summary>
/// <param name="projectStatusMapper">Mapper used to convert persisted project statuses.</param>
internal sealed class ProjectionMapper(IProjectStatusMapper projectStatusMapper) : IProjectionMapper
{
    private readonly IProjectStatusMapper _projectStatusMapper = projectStatusMapper;

    /// <inheritdoc />
    public ProjectStatus MapProjectStatus(ProjectProcessingStatus status) =>
        _projectStatusMapper.Map(status, ProjectStatusMappingExceptionStyle.Persisted);

    /// <inheritdoc />
    public RunStatus MapAgentStatus(
        PersistedAgentStatus status,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted) => status switch
    {
        PersistedAgentStatus.Waiting => RunStatus.Waiting,
        PersistedAgentStatus.Running => RunStatus.Running,
        PersistedAgentStatus.Completed => RunStatus.Completed,
        PersistedAgentStatus.Degraded => RunStatus.Degraded,
        _ => throw new InvalidOperationException(exceptionStyle switch
        {
            ExceptionStyle.Snapshot => $"Unsupported agent status '{status}'.",
            _ => $"Unsupported persisted agent status '{status}'.",
        }),
    };

    /// <inheritdoc />
    public TimelineEntryKind MapTimelineEntryKind(
        ProjectAgentTimelineEntryType entryType,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted) => entryType switch
    {
        ProjectAgentTimelineEntryType.Input => TimelineEntryKind.Input,
        ProjectAgentTimelineEntryType.Output => TimelineEntryKind.Output,
        ProjectAgentTimelineEntryType.Tool => TimelineEntryKind.Tool,
        ProjectAgentTimelineEntryType.Compaction => TimelineEntryKind.Compaction,
        _ => throw new InvalidOperationException(exceptionStyle switch
        {
            ExceptionStyle.Snapshot => $"Unsupported timeline entry type '{entryType}'.",
            _ => $"Unsupported persisted timeline entry type '{entryType}'.",
        }),
    };

    /// <inheritdoc />
    public GroupLiveDto MapGroup(GroupProjection group) => new()
    {
        GroupId = group.GroupId,
        RuntimeKey = group.RuntimeKey,
        DisplayName = group.DisplayName,
        CreatedAtUtc = group.CreatedAtUtc,
    };

    /// <inheritdoc />
    public LiveDto MapAgent(
        AgentProjection agent,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted) => new()
    {
        AgentId = agent.AgentId,
        GroupId = agent.GroupId,
        RuntimeKey = agent.RuntimeKey,
        DisplayName = agent.DisplayName,
        SystemPrompt = agent.SystemPrompt,
        Status = MapAgentStatus(agent.Status, exceptionStyle),
        CreatedAtUtc = agent.CreatedAtUtc,
    };

    /// <inheritdoc />
    public TimelineEntryDto MapTimelineEntry(
        TimelineEntryProjection entry,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted) => new()
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
