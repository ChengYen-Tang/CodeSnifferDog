using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

/// <summary>
/// Maps internal project read models into shared DTOs used by API responses and sidebar projections.
/// </summary>
internal interface IProjectProjectionMapper
{
    /// <summary>
    /// Maps a persisted processing status to the shared project status enum.
    /// </summary>
    /// <param name="status">Persisted processing status.</param>
    /// <returns>The shared project status.</returns>
    ProjectStatus MapStatus(ProjectProcessingStatus status);

    /// <summary>
    /// Maps a project summary projection to the shared summary DTO.
    /// </summary>
    /// <param name="project">Read model containing summary fields.</param>
    /// <returns>The mapped summary DTO.</returns>
    ProjectSummaryDto MapSummary(ProjectSummaryProjection project);

    /// <summary>
    /// Maps a list-item projection to the shared list-item DTO.
    /// </summary>
    /// <param name="project">Read model containing list-item fields.</param>
    /// <returns>The mapped list-item DTO.</returns>
    ProjectListItemDto MapListItem(ProjectListItemProjection project);

    /// <summary>
    /// Maps a sidebar projection to the shared sidebar project DTO.
    /// </summary>
    /// <param name="project">Sidebar projection read model.</param>
    /// <param name="status">Mapped shared project status.</param>
    /// <param name="sortOrder">Sort order inside the group.</param>
    /// <returns>The mapped sidebar project DTO.</returns>
    ProjectSidebarProjectDto MapSidebarProject(ProjectProjection project, ProjectStatus status, int sortOrder);
}
