using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar;

public interface ISnapshotService
{
    Task<ProjectSidebarSnapshotDto> GetSnapshotAsync(Guid? selectedProjectId, CancellationToken cancellationToken = default);
}
