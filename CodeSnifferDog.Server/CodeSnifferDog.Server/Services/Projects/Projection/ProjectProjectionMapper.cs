using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

/// <summary>
/// Maps internal project projections to shared DTOs consumed by API and client layers.
/// </summary>
internal sealed class ProjectProjectionMapper : IProjectProjectionMapper
{
    private readonly IProjectStatusMapper _statusMapper;

    /// <summary>
    /// Creates the mapper with the status mapper used for persisted statuses.
    /// </summary>
    /// <param name="statusMapper">Status mapper used to convert persisted processing states.</param>
    public ProjectProjectionMapper(IProjectStatusMapper statusMapper)
    {
        _statusMapper = statusMapper;
    }

    /// <inheritdoc />
    public ProjectStatus MapStatus(ProjectProcessingStatus status) => _statusMapper.Map(status);

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ProjectListItemDto MapListItem(ProjectListItemProjection project) => new()
    {
        ProjectId = project.ProjectId,
        OriginalFileName = project.OriginalFileName,
        Status = MapStatus(project.Status),
        CreatedAtUtc = project.CreatedAtUtc,
    };

    /// <inheritdoc />
    public ProjectSidebarProjectDto MapSidebarProject(
        ProjectProjection project,
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
