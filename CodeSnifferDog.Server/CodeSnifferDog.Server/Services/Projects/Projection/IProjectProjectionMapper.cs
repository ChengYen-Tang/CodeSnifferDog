using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

internal interface IProjectProjectionMapper
{
    ProjectStatus MapStatus(ProjectProcessingStatus status);

    ProjectSummaryDto MapSummary(ProjectRecord project);

    ProjectListItemDto MapListItem(ProjectRecord project);

    ProjectSidebarProjectDto MapSidebarProject(ProjectSidebarProjectProjection project, int sortOrder);
}
