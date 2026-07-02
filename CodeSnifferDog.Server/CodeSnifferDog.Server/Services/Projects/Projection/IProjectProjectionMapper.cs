using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

internal interface IProjectProjectionMapper
{
    ProjectStatus MapStatus(ProjectProcessingStatus status);

    ProjectSummaryDto MapSummary(ProjectSummaryProjection project);

    ProjectListItemDto MapListItem(ProjectListItemProjection project);

    ProjectSidebarProjectDto MapSidebarProject(ProjectProjection project, ProjectStatus status, int sortOrder);
}
