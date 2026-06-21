using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

internal interface IProjectStatusMapper
{
    ProjectStatus Map(
        ProjectProcessingStatus status,
        ProjectStatusMappingExceptionStyle exceptionStyle = ProjectStatusMappingExceptionStyle.Surface);
}
