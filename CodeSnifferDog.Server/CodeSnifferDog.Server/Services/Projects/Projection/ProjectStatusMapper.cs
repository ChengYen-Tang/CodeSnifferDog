using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

internal sealed class ProjectStatusMapper : IProjectStatusMapper
{
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
