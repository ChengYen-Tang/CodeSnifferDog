using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

internal interface IProjectionMapper
{
    ProjectStatus MapProjectStatus(ProjectProcessingStatus status);

    RunStatus MapAgentStatus(
        PersistedAgentStatus status,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);

    TimelineEntryKind MapTimelineEntryKind(
        ProjectAgentTimelineEntryType entryType,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);

    GroupLiveDto MapGroup(GroupProjection group);

    LiveDto MapAgent(
        AgentProjection agent,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);

    TimelineEntryDto MapTimelineEntry(
        TimelineEntryProjection entry,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);
}
