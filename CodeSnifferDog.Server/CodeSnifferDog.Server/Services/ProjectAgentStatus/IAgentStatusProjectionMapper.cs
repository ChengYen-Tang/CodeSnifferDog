using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

internal interface IAgentStatusProjectionMapper
{
    ProjectStatus MapProjectStatus(ProjectProcessingStatus status);

    ProjectAgentRunStatus MapAgentStatus(PersistedAgentStatus status);

    ProjectAgentTimelineEntryKind MapTimelineEntryKind(ProjectAgentTimelineEntryType entryType);

    ProjectAgentGroupLiveDto MapGroup(ProjectAgentGroupRecord group);

    ProjectAgentLiveDto MapAgent(ProjectAgentRecord agent);

    ProjectAgentTimelineEntryDto MapTimelineEntry(ProjectAgentTimelineEntryRecord entry);
}
