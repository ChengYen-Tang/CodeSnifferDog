using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

internal sealed class ProjectProjectionMapper : IProjectProjectionMapper
{
    private readonly IProjectStatusMapper _statusMapper;

    public ProjectProjectionMapper(IProjectStatusMapper statusMapper)
    {
        _statusMapper = statusMapper;
    }

    public ProjectStatus MapStatus(ProjectProcessingStatus status) => _statusMapper.Map(status);

    public ProjectSummaryDto MapSummary(ProjectSummaryProjection project) => new()
    {
        ProjectId = project.ProjectId,
        OriginalFileName = project.OriginalFileName,
        Status = MapStatus(project.Status),
        FileSizeBytes = project.FileSizeBytes,
        CreatedAtUtc = project.CreatedAtUtc,
        UpdatedAtUtc = project.UpdatedAtUtc,
        QueueTimestampUtc = project.QueueTimestampUtc,
        ProcessingStartedAtUtc = project.ProcessingStartedAtUtc,
        FinishedAtUtc = project.FinishedAtUtc,
        FailureReason = project.FailureReason,
    };

    public ProjectListItemDto MapListItem(ProjectListItemProjection project) => new()
    {
        ProjectId = project.ProjectId,
        OriginalFileName = project.OriginalFileName,
        Status = MapStatus(project.Status),
        CreatedAtUtc = project.CreatedAtUtc,
    };

    public ProjectSidebarProjectDto MapSidebarProject(
        ProjectSidebarProjectProjection project,
        ProjectStatus status,
        int sortOrder) => new()
    {
        ProjectId = project.ProjectId,
        OriginalFileName = project.OriginalFileName,
        Status = status,
        CreatedAtUtc = project.CreatedAtUtc,
        SortOrder = sortOrder,
    };
}
