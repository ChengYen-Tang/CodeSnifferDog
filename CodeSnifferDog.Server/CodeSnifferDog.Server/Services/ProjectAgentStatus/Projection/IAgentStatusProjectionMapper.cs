using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

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
