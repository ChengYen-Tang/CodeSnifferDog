using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

/// <summary>
/// Maps persisted project processing states to shared project statuses.
/// </summary>
internal sealed class ProjectStatusMapper : IProjectStatusMapper
{
    /// <inheritdoc />
    public ProjectStatus Map(
        ProjectProcessingStatus status,
        ProjectStatusMappingExceptionStyle exceptionStyle = ProjectStatusMappingExceptionStyle.Surface) => status switch
    {
        ProjectProcessingStatus.Queued => ProjectStatus.Queued,
        ProjectProcessingStatus.Reviewing => ProjectStatus.Reviewing,
        ProjectProcessingStatus.Completed => ProjectStatus.Completed,
        ProjectProcessingStatus.Failed => ProjectStatus.Failed,
        ProjectProcessingStatus.Canceled => ProjectStatus.Canceled,
        _ => throw CreateUnsupportedStatusException(status, exceptionStyle),
    };

    /// <summary>
    /// Creates the exception thrown for an unsupported persisted project status.
    /// </summary>
    /// <param name="status">Unsupported persisted status.</param>
    /// <param name="exceptionStyle">Exception style to create.</param>
    /// <returns>The created exception.</returns>
    private static Exception CreateUnsupportedStatusException(
        ProjectProcessingStatus status,
        ProjectStatusMappingExceptionStyle exceptionStyle) =>
        exceptionStyle switch
        {
            ProjectStatusMappingExceptionStyle.Persisted =>
                new InvalidOperationException($"Unsupported project status '{status}'."),
            _ => new ArgumentOutOfRangeException(nameof(status), status, "Unsupported project status."),
        };
}
