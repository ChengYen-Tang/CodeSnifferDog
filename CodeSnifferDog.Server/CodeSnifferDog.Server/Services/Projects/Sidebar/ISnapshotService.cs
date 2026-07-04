using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Services.Projects.Sidebar;

/// <summary>
/// Builds sidebar snapshots consumed by the server prerender path and client refreshes.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Builds the current sidebar snapshot.
    /// </summary>
    /// <param name="selectedProjectId">Optional project identifier currently selected by the client.</param>
    /// <param name="cancellationToken">Cancels snapshot loading.</param>
    /// <returns>The sidebar snapshot.</returns>
    Task<ProjectSidebarSnapshotDto> GetSnapshotAsync(Guid? selectedProjectId, CancellationToken cancellationToken = default);
}
