using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

internal interface IProjectionMapper
{
    ProjectStatus MapProjectStatus(ProjectProcessingStatus status);

    ProjectAgentRunStatus MapAgentStatus(
        PersistedAgentStatus status,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);

    ProjectAgentTimelineEntryKind MapTimelineEntryKind(
        ProjectAgentTimelineEntryType entryType,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);

    ProjectAgentGroupLiveDto MapGroup(GroupProjection group);

    ProjectAgentLiveDto MapAgent(
        AgentProjection agent,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);

    ProjectAgentTimelineEntryDto MapTimelineEntry(
        TimelineEntryProjection entry,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);
}
