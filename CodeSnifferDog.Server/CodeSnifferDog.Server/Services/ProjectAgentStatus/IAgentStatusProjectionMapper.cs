using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

internal interface IAgentStatusProjectionMapper
{
    ProjectStatus MapProjectStatus(ProjectProcessingStatus status);

    ProjectAgentRunStatus MapAgentStatus(
        PersistedAgentStatus status,
        AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted);

    ProjectAgentTimelineEntryKind MapTimelineEntryKind(
        ProjectAgentTimelineEntryType entryType,
        AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted);

    ProjectAgentGroupLiveDto MapGroup(AgentStatusGroupProjection group);

    ProjectAgentLiveDto MapAgent(
        AgentStatusAgentProjection agent,
        AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted);

    ProjectAgentTimelineEntryDto MapTimelineEntry(
        AgentStatusTimelineEntryProjection entry,
        AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted);
}

internal enum AgentStatusProjectionExceptionStyle
{
    Persisted,
    Snapshot,
}

internal sealed record AgentStatusGroupProjection(
    Guid GroupId,
    string RuntimeKey,
    string DisplayName,
    DateTimeOffset CreatedAtUtc);

internal sealed record AgentStatusAgentProjection(
    Guid AgentId,
    Guid GroupId,
    string RuntimeKey,
    string DisplayName,
    string SystemPrompt,
    PersistedAgentStatus Status,
    DateTimeOffset CreatedAtUtc);

internal sealed record AgentStatusTimelineEntryProjection(
    Guid TimelineEntryId,
    Guid AgentId,
    long Sequence,
    ProjectAgentTimelineEntryType EntryType,
    DateTimeOffset OccurredAtUtc,
    string? Message,
    string? ToolCallId,
    string? ToolName,
    string? ToolArguments,
    string? ToolResult);
