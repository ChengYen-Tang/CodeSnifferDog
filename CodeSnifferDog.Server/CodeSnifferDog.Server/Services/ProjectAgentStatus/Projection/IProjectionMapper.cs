using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

/// <summary>
/// Maps persisted project-agent status entities into shared snapshot and live-update DTOs.
/// </summary>
internal interface IProjectionMapper
{
    /// <summary>
    /// Maps a persisted project processing status to the shared project status enum.
    /// </summary>
    /// <param name="status">Persisted project processing status.</param>
    /// <returns>The mapped shared project status.</returns>
    ProjectStatus MapProjectStatus(ProjectProcessingStatus status);

    /// <summary>
    /// Maps a persisted agent status to the shared run-status enum.
    /// </summary>
    /// <param name="status">Persisted agent status.</param>
    /// <param name="exceptionStyle">Exception wording used when the status is unsupported.</param>
    /// <returns>The mapped shared run status.</returns>
    RunStatus MapAgentStatus(
        PersistedAgentStatus status,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);

    /// <summary>
    /// Maps a persisted timeline entry type to the shared timeline entry kind.
    /// </summary>
    /// <param name="entryType">Persisted timeline entry type.</param>
    /// <param name="exceptionStyle">Exception wording used when the entry type is unsupported.</param>
    /// <returns>The mapped shared timeline entry kind.</returns>
    TimelineEntryKind MapTimelineEntryKind(
        ProjectAgentTimelineEntryType entryType,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);

    /// <summary>
    /// Maps a group projection to the shared live group DTO.
    /// </summary>
    /// <param name="group">Persisted group projection.</param>
    /// <returns>The mapped group DTO.</returns>
    GroupLiveDto MapGroup(GroupProjection group);

    /// <summary>
    /// Maps an agent projection to the shared live agent DTO.
    /// </summary>
    /// <param name="agent">Persisted agent projection.</param>
    /// <param name="exceptionStyle">Exception wording used when persisted values are unsupported.</param>
    /// <returns>The mapped agent DTO.</returns>
    LiveDto MapAgent(
        AgentProjection agent,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);

    /// <summary>
    /// Maps a timeline-entry projection to the shared timeline entry DTO.
    /// </summary>
    /// <param name="entry">Persisted timeline-entry projection.</param>
    /// <param name="exceptionStyle">Exception wording used when persisted values are unsupported.</param>
    /// <returns>The mapped timeline entry DTO.</returns>
    TimelineEntryDto MapTimelineEntry(
        TimelineEntryProjection entry,
        ExceptionStyle exceptionStyle = ExceptionStyle.Persisted);
}
