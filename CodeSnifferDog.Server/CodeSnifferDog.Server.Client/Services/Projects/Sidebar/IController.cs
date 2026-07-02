using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

public interface IController
{
    State Current { get; }

    event Action? Changed;

    void InitializeSnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri);

    void SelectProject(string projectId);

    void ToggleGroup(string groupKey, ProjectStatus status);

    void SyncSelectedProjectFromUri(string? selectedProjectIdFromUri);

    Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<bool> CancelProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task StartAsync(
        ProjectSidebarSnapshotDto? initialSnapshot = null,
        string? selectedProjectIdFromUri = null,
        CancellationToken cancellationToken = default);
}
