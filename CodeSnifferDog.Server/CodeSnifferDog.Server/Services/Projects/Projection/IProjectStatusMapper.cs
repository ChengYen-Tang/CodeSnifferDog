using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

/// <summary>
/// Maps persisted project processing states to shared project statuses.
/// </summary>
internal interface IProjectStatusMapper
{
    /// <summary>
    /// Maps a persisted processing status to the shared project status enum.
    /// </summary>
    /// <param name="status">Persisted processing status.</param>
    /// <param name="exceptionStyle">Exception style used when the status is unsupported.</param>
    /// <returns>The mapped shared project status.</returns>
    ProjectStatus Map(
        ProjectProcessingStatus status,
        ProjectStatusMappingExceptionStyle exceptionStyle = ProjectStatusMappingExceptionStyle.Surface);
}
